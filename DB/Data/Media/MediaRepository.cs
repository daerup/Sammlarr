using Contracts;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class MediaRepository(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	MediaDetailsFactory detailsFactory,
	MediaWriteLock writeLock,
	IDateTimeProvider clock
) : IMediaRepository
{
	public async Task<IReadOnlyList<StoredMedia>> GetAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default)
	{
		if (ids.Count == 0)
		{
			return [];
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var snapshot = await ctx.SnapshotAsync(cancellationToken);
		return await ReadAsync(ctx, [.. ids.Select(id => id.Value).Distinct()], cancellationToken);
	}

	public async Task<IReadOnlyList<StoredMedia>> GetByExternalsAsync(IReadOnlyList<IExternalMediaId> candidates, CancellationToken cancellationToken = default)
	{
		if (candidates.Count == 0)
		{
			return [];
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var snapshot = await ctx.SnapshotAsync(cancellationToken);
		return await ReadAsync(ctx, await ctx.OwnersAsync(candidates, cancellationToken), cancellationToken);
	}

	private async Task<IReadOnlyList<StoredMedia>> ReadAsync(SammlarrDbContext ctx, IReadOnlyList<Guid> owners, CancellationToken cancellationToken)
	{
		if (owners.Count == 0)
		{
			return [];
		}

		var externals = await ctx
			.ExternalMediaIds.AsNoTracking()
			.Where(external => EF.Parameter(owners).Contains(EF.Property<Guid>(external, MediaQueries.MediaIdColumn)))
			.Select(external => new { MediaId = EF.Property<Guid>(external, MediaQueries.MediaIdColumn), Row = external })
			.ToListAsync(cancellationToken);
		var details = await ctx
			.MediaDetails.AsNoTracking()
			.Where(row => EF.Parameter(owners).Contains(row.MediaId))
			.ToDictionaryAsync(row => row.MediaId, cancellationToken);
		var ratings = await ctx
			.MediaRatings.AsNoTracking()
			.Where(rating => EF.Parameter(owners).Contains(EF.Property<Guid>(rating, MediaQueries.MediaIdColumn)))
			.Select(rating => new { MediaId = EF.Property<Guid>(rating, MediaQueries.MediaIdColumn), Row = rating })
			.ToListAsync(cancellationToken);
		var answers = await ctx
			.ProviderAnswers.AsNoTracking()
			.Where(answer => EF.Parameter(owners).Contains(EF.Property<Guid>(answer, MediaQueries.MediaIdColumn)))
			.Select(answer => new { MediaId = EF.Property<Guid>(answer, MediaQueries.MediaIdColumn), Row = answer })
			.ToListAsync(cancellationToken);
		var links = await ctx.Set<MediaPageLinkRow>()
			.AsNoTracking()
			.Where(link => EF.Parameter(owners).Contains(EF.Property<Guid>(link, MediaQueries.MediaIdColumn)))
			.Select(link => new { MediaId = EF.Property<Guid>(link, MediaQueries.MediaIdColumn), Row = link })
			.ToListAsync(cancellationToken);
		ratings.ForEach(rating => details[rating.MediaId].Ratings.Add(rating.Row));
		answers.ForEach(answer => details[answer.MediaId].Answers.Add(answer.Row));
		links.ForEach(link => details[link.MediaId].PageLinks.Add(link.Row));

		return
		[
			.. from external in externals
			group external.Row by external.MediaId into owned
			select new StoredMedia(
				new MediaIdentity(MediaId.From(owned.Key), [.. owned.Select(row => (IIdentityKey)row.ToExternalMediaId())]),
				details.GetValueOrNone(owned.Key).Map(detailsFactory.ToCached)
			),
		];
	}

	public async Task SaveDetailsAsync(IReadOnlyList<DetailsChange> changes, CancellationToken cancellationToken = default)
	{
		var changed = changes.Where(change => change.Changed).GroupBy(change => change.Id).Select(group => group.Last()).ToList();
		if (changed.Count == 0)
		{
			return;
		}

		var ids = changed.Select(change => change.Id.Value).ToList();
		using var _ = await writeLock.AcquireAsync(cancellationToken);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
		var stored = await ctx
			.MediaDetails.Where(details => EF.Parameter(ids).Contains(details.MediaId))
			.Select(details => details.MediaId)
			.ToHashSetAsync(cancellationToken);
		await ctx
			.MediaRatings.Where(rating => EF.Parameter(ids).Contains(EF.Property<Guid>(rating, MediaQueries.MediaIdColumn)))
			.ExecuteDeleteAsync(cancellationToken);
		await ctx
			.ProviderAnswers.Where(answer => EF.Parameter(ids).Contains(EF.Property<Guid>(answer, MediaQueries.MediaIdColumn)))
			.ExecuteDeleteAsync(cancellationToken);
		await ctx.Set<MediaPageLinkRow>()
			.Where(link => EF.Parameter(ids).Contains(EF.Property<Guid>(link, MediaQueries.MediaIdColumn)))
			.ExecuteDeleteAsync(cancellationToken);
		foreach (var change in changed)
		{
			var row = detailsFactory.BuildFrom(change.After, change.Id.Value);
			ctx.Entry(row).State = stored.Contains(row.MediaId) ? EntityState.Modified : EntityState.Added;
			ctx.AddRange(row.Ratings);
			ctx.AddRange(row.Answers);
			ctx.AddRange(row.PageLinks);
		}

		await ctx.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
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
		await ctx.TrimDetailsAsync(ctx.MediaDetails.Where(m => rawIds.Contains(m.MediaId)), clock.Now, cancellationToken);
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
}
