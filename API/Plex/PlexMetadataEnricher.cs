using Contracts.Media.Metadata;

namespace API.Plex;

internal class PlexMetadataEnricher(PlexApi plexApi) : IMetadataEnricher
{
	public Task<MediaLookup?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		if (id is not PlexId plexId)
		{
			return Task.FromResult<MediaLookup?>(null);
		}
		return LookUpInternalAsync(plexId, cancellationToken);
	}

	private async Task<MediaLookup?> LookUpInternalAsync(PlexId plexId, CancellationToken cancellationToken) =>
		await plexApi.ByIdAsync(plexId, cancellationToken);
}
