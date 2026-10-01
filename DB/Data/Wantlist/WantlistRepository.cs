using Contracts.Wantlist;
using Domain.Feed;
using Domain.Media;
using Domain.Wantlist;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Wantlist;

internal sealed class WantlistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IWantlistRepository
{
	public async Task<IReadOnlyList<WantlistItem>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from wantlistItem in ctx.Wantlist.AsNoTrackingWithIdentityResolution().Include(w => w.SourceFeed).Include(w => w.SourceRun)
			select new { WantlistItem = wantlistItem, MediaIdValue = wantlistItem.Media.Id }
		).ToListAsync(cancellationToken);

		return [.. rows.Select(row => new WantlistItem(MediaId.From(row.MediaIdValue), row.WantlistItem.CreatedAt, row.WantlistItem.ToProvenance()))];
	}

	public async Task<IReadOnlyDictionary<FeedId, int>> CountByFeedAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var counts = await ctx
			.Wantlist.AsNoTracking()
			.Where(w => w.Origin == WantlistItemOrigin.Feed && w.SourceFeedId != null)
			.GroupBy(w => w.SourceFeedId!.Value)
			.Select(group => new { FeedId = group.Key, Count = group.Count() })
			.ToListAsync(cancellationToken);

		return counts.ToDictionary(count => FeedId.From(count.FeedId), count => count.Count);
	}

	public async Task<IReadOnlyList<MediaId>> AddAsync(IEnumerable<MediaId> ids, WantlistItemProvenance provenance, CancellationToken cancellationToken = default)
	{
		var distinct = ids.Distinct().ToList();
		if (distinct.Count == 0)
		{
			return [];
		}

		var rawIds = distinct.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var matchedMedia = await ctx.Media.Where(m => rawIds.Contains(m.Id)).ToListAsync(cancellationToken);
		var existingMediaIds = await ctx.Wantlist.Where(w => rawIds.Contains(w.Media.Id)).Select(w => w.Media.Id).ToHashSetAsync(cancellationToken);

		var inserted = matchedMedia.Where(m => !existingMediaIds.Contains(m.Id)).ToList();
		foreach (var media in inserted)
		{
			ctx.Wantlist.Add(WantlistItemMapping.ToRow(media, provenance));
		}

		await ctx.SaveChangesAsync(cancellationToken);
		return [.. inserted.Select(m => MediaId.From(m.Id))];
	}

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var distinct = ids.Distinct().ToList();
		if (distinct.Count == 0)
		{
			return [];
		}
		var rawIds = distinct.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var removed = await ctx.Wantlist.Where(w => rawIds.Contains(w.Media.Id)).Select(w => w.Media.Id).ToListAsync(cancellationToken);
		await ctx.Wantlist.Where(w => rawIds.Contains(w.Media.Id)).ExecuteDeleteAsync(cancellationToken);
		return [.. removed.Select(MediaId.From)];
	}

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, FeedId addedBy, CancellationToken cancellationToken = default)
	{
		var distinct = ids.Distinct().ToList();
		if (distinct.Count == 0)
		{
			return [];
		}
		var rawIds = distinct.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var added = ctx.Wantlist.Where(w => rawIds.Contains(w.Media.Id) && w.SourceFeedId == addedBy.Value && w.Origin == WantlistItemOrigin.Feed);
		var removed = await added.Select(w => w.Media.Id).ToListAsync(cancellationToken);
		await added.ExecuteDeleteAsync(cancellationToken);
		return [.. removed.Select(MediaId.From)];
	}

	public async Task OrphanAsync(Domain.Feed.Feed addedBy, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var feedId = addedBy.Id.Value;
		var name = addedBy.Name;
		await ctx
			.Wantlist.Where(w => w.SourceFeedId == feedId)
			.ExecuteUpdateAsync(
				s =>
					s.SetProperty(w => w.Origin, WantlistItemOrigin.Orphaned)
						.SetProperty(w => w.SourceFeedNameSnapshot, name)
						.SetProperty(w => w.SourceRunId, (Guid?)null),
				cancellationToken
			);
	}
}
