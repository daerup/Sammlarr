using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;

namespace API.Plex;

internal class PlexMetadataEnricher(PlexApi plexApi) : IMetadataEnricher
{
	public Task<IMediaDetail?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		if (id is not PlexId)
		{
			return Task.FromResult<IMediaDetail?>(null);
		}
		return LookUpInternalAsync(id, cancellationToken);
	}

	private async Task<IMediaDetail?> LookUpInternalAsync(IExternalMediaId id, CancellationToken cancellationToken) =>
		await plexApi.LookUpAsync(id, cancellationToken);
}
