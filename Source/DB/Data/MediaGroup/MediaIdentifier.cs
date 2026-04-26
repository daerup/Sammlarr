using Contracts.Media;

namespace DB.Data.MediaGroup;

public readonly record struct MediaIdentifier(MediaProvider Source, string ExternalId, MediaType MediaType)
{
	public static MediaIdentifier From(IMediaId mediaId) => new(mediaId.Source, mediaId.Value, mediaId.Type);

	public IMediaId ToMediaId() =>
		Source switch
		{
			MediaProvider.Tmdb => TmdbId.From(ExternalId, MediaType),
			MediaProvider.Imdb => ImdbId.From(ExternalId, MediaType),
			MediaProvider.Tvdb => TvdbId.From(ExternalId, MediaType),
			MediaProvider.Plex => PlexId.From(ExternalId, MediaType),
		};
}
