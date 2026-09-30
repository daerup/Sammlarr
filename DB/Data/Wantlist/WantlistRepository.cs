using Contracts.Wantlist;
using Domain.Feed;
using Domain.Media;
using Domain.Wantlist;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Wantlist;

internal sealed class WantlistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IWantlistRepository
{
	public async Task<IReadOnlyList<WantlistItem>> GetEntriesAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from wantlistItem in ctx.Wantlist.AsNoTrackingWithIdentityResolution().Include(w => w.SourceFeed).Include(w => w.SourceRun)
			select new { WantlistItem = wantlistItem, MediaIdValue = wantlistItem.Media.Id }
		).ToListAsync(cancellationToken);

		return [.. rows.Select(row => new WantlistItem(MediaId.From(row.MediaIdValue), row.WantlistItem.CreatedAt, row.WantlistItem.ToProvenance()))];
	}

	public async Task<IReadOnlyList<MediaId>> GetIdsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var values = await ctx.Wantlist.AsNoTracking().Select(w => w.Media.Id).ToListAsync(cancellationToken);
		return [.. values.Select(MediaId.From)];
	}

	public async Task<IReadOnlyDictionary<FeedId, int>> GetOwnedCountsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var counts = await ctx
			.Wantlist.AsNoTracking()
			.Where(w => w.Origin == WantlistItemOrigin.Feed && w.SourceFeedId != null)
			.GroupBy(w => w.SourceFeedId!.Value)
			.Select(group => new { FeedId = group.Key, Count = group.Count() })
			.ToListAsync(cancellationToken);

		return counts.ToDictionary(entry => FeedId.From(entry.FeedId), entry => entry.Count);
	}

	public async Task<IReadOnlyList<MediaId>> AddAsync(
		IEnumerable<MediaId> mediaIds,
		WantlistItemProvenance provenance,
		CancellationToken cancellationToken = default
	)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return [];
		}

		var rawIds = ids.Select(id => id.Value).ToList();

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

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return [];
		}
		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var removed = await ctx.Wantlist.Where(w => rawIds.Contains(w.Media.Id)).Select(w => w.Media.Id).ToListAsync(cancellationToken);
		await ctx.Wantlist.Where(w => rawIds.Contains(w.Media.Id)).ExecuteDeleteAsync(cancellationToken);
		return [.. removed.Select(MediaId.From)];
	}

	public async Task<IReadOnlyList<MediaId>> RemoveOwnedAsync(IEnumerable<MediaId> mediaIds, FeedId owner, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return [];
		}
		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var owned = ctx.Wantlist.Where(w => rawIds.Contains(w.Media.Id) && w.SourceFeedId == owner.Value && w.Origin == WantlistItemOrigin.Feed);
		var removed = await owned.Select(w => w.Media.Id).ToListAsync(cancellationToken);
		await owned.ExecuteDeleteAsync(cancellationToken);
		return [.. removed.Select(MediaId.From)];
	}
}
