using Contracts.Media;
using Contracts.Watchlist.Sync;
using Domain.Media;
using Shared;
using UI.Shared.Media;

namespace UI.Features.Watchlist.Components;

internal static class DryRunMetadataLoader
{
	public static async Task<Dictionary<MediaId, MediaLookup>> LoadAsync(
		IMediaService media,
		WatchlistSyncDryRun dryRun,
		int limit,
		CancellationToken cancellationToken
	)
	{
		var metadata = new Dictionary<MediaId, MediaLookup>();
		var ids = dryRun.Changes.Take(limit).Select(c => c.MediaId);

		await foreach (var result in media.GetDetailsAsync(ids, cancellationToken))
		{
			if (result is not IdentifiedMedia detail)
			{
				continue;
			}
			metadata[detail.Id] = detail;
		}
		return metadata;
	}
}
