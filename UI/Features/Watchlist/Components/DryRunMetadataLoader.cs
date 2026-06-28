using Contracts.Media;
using Contracts.Watchlist.Sync;
using Shared;
using UI.Shared.Media;

namespace UI.Features.Watchlist.Components;

internal static class DryRunMetadataLoader
{
	public static async Task<Dictionary<MediaId, MediaMatch>> LoadAsync(
		IMediaService media,
		WatchlistSyncDryRun dryRun,
		int limit,
		CancellationToken cancellationToken
	)
	{
		var metadata = new Dictionary<MediaId, MediaMatch>();
		var ids = dryRun.Changes.Take(limit).Select(c => c.MediaId);

		await foreach (var result in media.GetDetailsAsync(ids, cancellationToken))
		{
			if (result is not MediaMatch detail)
			{
				continue;
			}
			metadata[detail.Id] = detail;
		}
		return metadata;
	}
}
