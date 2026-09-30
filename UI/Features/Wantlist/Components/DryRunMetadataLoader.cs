using Contracts.Media;
using Domain.Media;
using Domain.Wantlist.Sync;

namespace UI.Features.Wantlist.Components;

internal static class DryRunMetadataLoader
{
	public static async Task<Dictionary<MediaId, MediaLookup>> LoadAsync(IMediaService media, WantlistSyncSummary dryRun, CancellationToken cancellationToken)
	{
		var metadata = new Dictionary<MediaId, MediaLookup>();
		var ids = dryRun.Changes.Select(c => c.MediaId).Concat(dryRun.NoOps).Distinct();

		await foreach (var detail in media.GetDetailsAsync(ids, cancellationToken).WhereSuccessAwait())
		{
			metadata[detail.Id] = detail;
		}
		return metadata;
	}
}
