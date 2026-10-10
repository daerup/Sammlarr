using System.Data;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class MediaRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, MediaDetailsFactory detailsFactory, MediaWriteLock writeLock)
	: IMediaRepository
{
	public async Task<IReadOnlyList<StoredMedia>> GetAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default)
	{
		if (ids.Count == 0)
		{
			return [];
		}
		var rawIds = ids.Select(id => id.Value).ToList();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await QueryStoredAsync(ctx, ctx.Media.Where(m => rawIds.Contains(m.Id)), cancellationToken);
	}

	public async Task<IReadOnlyList<StoredMedia>> GetByExternalsAsync(IReadOnlyList<IExternalMediaId> candidates, CancellationToken cancellationToken = default)
	{
		if (candidates.Count == 0)
		{
			return [];
		}
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var mediaIds = ctx.ExternalMediaIds.WhereMatchingAny(candidates).Select(e => e.Media.Id).Distinct();
		return await QueryStoredAsync(ctx, ctx.Media.Where(m => mediaIds.Contains(m.Id)), cancellationToken);
	}

	private async Task<IReadOnlyList<StoredMedia>> QueryStoredAsync(SammlarrDbContext ctx, IQueryable<MediaRow> media, CancellationToken cancellationToken)
	{
		var rows = await media
			.AsNoTracking()
			.Include(m => m.ExternalIds)
			.Include(m => m.Details!)
				.ThenInclude(details => details.Ratings)
			.Include(m => m.Details!)
				.ThenInclude(details => details.Answers)
			.AsSingleQuery()
			.ToListAsync(cancellationToken);
		var links = await PageLinksAsync(ctx, [.. rows.Select(row => row.Id)], cancellationToken);
		foreach (var row in rows)
		{
			Optional.Of(row.Details).Apply(details => details.PageLinks = [.. links[row.Id]]);
		}

		return [.. rows.Select(row => new StoredMedia(ToIdentity(row), Optional.Of(row.Details).Map(detailsFactory.ToCached)))];
	}

	private static async Task<ILookup<Guid, MediaPageLinkRow>> PageLinksAsync(SammlarrDbContext ctx, List<Guid> mediaIds, CancellationToken cancellationToken)
	{
		var links = await ctx.Set<MediaPageLinkRow>()
			.AsNoTracking()
			.Where(link => mediaIds.Contains(EF.Property<Guid>(link, "MediaId")))
			.Select(link => new { MediaId = EF.Property<Guid>(link, "MediaId"), Link = link })
			.ToListAsync(cancellationToken);
		return links.ToLookup(found => found.MediaId, found => found.Link);
	}

	public async Task UpsertDetailsAsync(IReadOnlyDictionary<MediaId, CachedDetails> details, CancellationToken cancellationToken = default)
	{
		if (details.Count == 0)
		{
			return;
		}

		using var _ = await writeLock.AcquireAsync(cancellationToken);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rawIds = details.Keys.Select(id => id.Value).ToList();
		var existing = await ctx
			.MediaDetails.Include(m => m.Ratings)
			.Include(m => m.Answers)
			.Include(m => m.PageLinks)
			.AsSingleQuery()
			.Where(m => rawIds.Contains(m.MediaId))
			.ToDictionaryAsync(m => m.MediaId, cancellationToken);

		foreach (var (id, cached) in details)
		{
			if (existing.TryGetValue(id.Value, out var entity))
			{
				detailsFactory.Update(entity, cached);
			}
			else
			{
				ctx.MediaDetails.Add(detailsFactory.BuildFrom(cached, id.Value));
			}
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task SavePageLinksAsync(MediaId id, IReadOnlyCollection<IPageLink> links, CancellationToken cancellationToken = default)
	{
		using var _ = await writeLock.AcquireAsync(cancellationToken);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var details = await ctx.MediaDetails.Include(m => m.PageLinks).Where(m => m.MediaId == id.Value).ToListAsync(cancellationToken);
		foreach (var row in details)
		{
			List<MediaPageLinkRow> added =
			[
				.. links.DistinctBy(link => link.Source).Where(link => row.PageLinks.All(held => held.Site != link.Source)).Select(link => link.ToPageLinkRow(row)),
			];
			row.PageLinks.AddRange(added);
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveDetailsAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default)
	{
		if (ids.Count == 0)
		{
			return;
		}

		var rawIds = ids.Select(id => id.Value).ToList();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.MediaDetails.Where(m => rawIds.Contains(m.MediaId)).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetExternalsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider source,
		CancellationToken cancellationToken = default
	)
	{
		var rawIds = ids.Distinct().Select(id => id.Value).ToList();
		if (rawIds.Count is 0)
		{
			return new Dictionary<MediaId, IExternalMediaId>();
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.ExternalMediaIds.AsNoTracking()
			.Where(e => e.Source == source && rawIds.Contains(e.Media.Id))
			.Select(e => new { MediaId = e.Media.Id, External = e })
			.ToListAsync(cancellationToken);
		return rows.GroupBy(r => r.MediaId)
			.ToDictionary(group => MediaId.From(group.Key), group => group.MinBy(r => r.External.Id, StringComparer.Ordinal)!.External.ToExternalMediaId());
	}

	private static MediaIdentity ToIdentity(MediaRow entity) =>
		new(MediaId.From(entity.Id), [.. entity.ExternalIds.Select(e => (IIdentityKey)e.ToExternalMediaId())]);
}
