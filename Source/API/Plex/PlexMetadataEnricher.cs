using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;

namespace API.Plex;

internal class PlexMetadataEnricher(PlexApi plexApi) : IMetadataEnricher
{
	public Task<IMediaDetailResult?> LookUpAsync(IMediaId id, CancellationToken cancellationToken = default)
	{
		if (id is not PlexId)
		{
			return Task.FromResult<IMediaDetailResult?>(null);
		}
		return LookUpInternalAsync(id, cancellationToken);
	}

	private async Task<IMediaDetailResult?> LookUpInternalAsync(IMediaId id, CancellationToken cancellationToken) =>
		await plexApi.LookUpAsync(id, cancellationToken);
}
