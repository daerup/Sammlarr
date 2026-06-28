using Contracts.Media.Metadata;

namespace API.Plex;

internal class PlexMetadataEnricher(PlexApi plexApi) : IMetadataEnricher
{
	public Task<MediaMatch?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		if (id is not PlexId plexId)
		{
			return Task.FromResult<MediaMatch?>(null);
		}
		return LookUpInternalAsync(plexId, cancellationToken);
	}

	private async Task<MediaMatch?> LookUpInternalAsync(PlexId plexId, CancellationToken cancellationToken) => await plexApi.ByIdAsync(plexId, cancellationToken);
}
