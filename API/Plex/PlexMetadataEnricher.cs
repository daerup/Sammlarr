using Contracts.Media.Metadata;
using Domain.Media;

namespace API.Plex;

internal class PlexMetadataEnricher(PlexApi plexApi) : IMetadataEnricher
{
	public Task<Result<MediaLookup>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default) =>
		id.Require<PlexId>("Plex enrichment requires a Plex id.").Map(plexId => plexApi.ByIdAsync(plexId, cancellationToken));
}
