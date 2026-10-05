using Domain.Media;
using Domain.Providers;

namespace DB.Data.Media;

internal static class MediaIdConversionExtensions
{
	public static IExternalMediaId ToExternalMediaId(this ExternalMediaIdRow externalId) => Create(externalId.Source, externalId.MediaType, externalId.Id);

	public static ExternalMediaIdRow ToExternalIdEntity(this IExternalMediaId mediaId, MediaRow media) =>
		new()
		{
			Source = mediaId.Source,
			MediaType = mediaId.Type,
			Id = mediaId.Value,
			Media = media,
		};

	private static IExternalMediaId Create(MediaProvider source, MediaType mediaType, string externalId) =>
		source switch
		{
			MediaProvider.Tmdb => TmdbId.From(externalId, mediaType),
			MediaProvider.Imdb => ImdbId.From(externalId, mediaType),
			MediaProvider.RottenTomatoes => RottenTomatoesId.From(externalId, mediaType),
			MediaProvider.Tvdb => TvdbId.From(externalId, mediaType),
			MediaProvider.Plex => PlexId.From(externalId, mediaType),
			MediaProvider.Mdblist => MdblistId.From(externalId, mediaType),
		};
}
