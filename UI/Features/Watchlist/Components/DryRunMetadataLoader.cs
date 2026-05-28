using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Watchlist.Sync;
using Shared;

namespace UI.Features.Watchlist.Components;

internal static class DryRunMetadataLoader
{
	public static async Task<Dictionary<MediaId, IMediaDetail>> LoadAsync(
		IMediaService media,
		WatchlistSyncDryRun dryRun,
		int limit,
		CancellationToken cancellationToken
	)
	{
		var metadata = new Dictionary<MediaId, IMediaDetail>();
		var ids = dryRun.Changes.Take(limit).Select(c => c.MediaId);

		await foreach (var result in media.GetDetailsAsync(ids, cancellationToken))
		{
			if (result is not IMediaDetail detail)
			{
				continue;
			}
			metadata[detail.Identity.Id] = detail;
		}
		return metadata;
	}
}
