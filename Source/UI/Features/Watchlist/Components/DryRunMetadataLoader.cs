using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Watchlist.Sync;
using Shared;

namespace UI.Features.Watchlist.Components;

internal static class DryRunMetadataLoader
{
	public static async Task<Dictionary<IMediaId, IMediaDetailResult>> LoadAsync(
		IMediaMetadataProvider metadataProvider,
		WatchlistSyncDryRunReport report,
		int limit,
		CancellationToken cancellationToken
	)
	{
		var metadata = new Dictionary<IMediaId, IMediaDetailResult>();
		var pipeline = report
			.Changes.Take(limit)
			.ToAsyncEnumerable()
			.SelectAwait(
				async change => (change.MediaId, Detail: await metadataProvider.GetAsync(change.MediaId, cancellationToken)),
				IMediaMetadataProvider.RecommendedConcurrency,
				cancellationToken
			);

		await foreach (var (id, detail) in pipeline)
		{
			metadata[id] = detail;
		}
		return metadata;
	}
}
