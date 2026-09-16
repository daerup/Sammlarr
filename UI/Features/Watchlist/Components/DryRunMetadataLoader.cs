using Contracts.Media;
using Domain.Media;
using Domain.Watchlist.Sync;

namespace UI.Features.Watchlist.Components;

internal static class DryRunMetadataLoader
{
	public static async Task<Dictionary<MediaId, MediaLookup>> LoadAsync(
		IMediaService media,
		WatchlistSyncSummary dryRun,
		int limit,
		CancellationToken cancellationToken
	)
	{
		var metadata = new Dictionary<MediaId, MediaLookup>();
		var ids = dryRun.Changes.Take(limit).Select(c => c.MediaId);

		await foreach (var detail in media.GetDetailsAsync(ids, cancellationToken).WhereSuccessAwait())
		{
			metadata[detail.Id] = detail;
		}
		return metadata;
	}
}
