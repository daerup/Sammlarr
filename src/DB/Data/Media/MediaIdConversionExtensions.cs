using Domain.Media;
using Domain.Providers;

namespace DB.Data.Media;

internal static class MediaIdConversionExtensions
{
	public static IExternalMediaId ToExternalMediaId(this ExternalMediaIdRow externalId) => Create(externalId.Source, externalId.MediaType, externalId.Id);

	public static ExternalMediaIdRow ToExternalIdEntity(this IIdentityKey mediaId, MediaRow media) =>
		new()
		{
			Source = mediaId.Source,
			MediaType = mediaId.Type,
			Id = mediaId.Value,
			Media = media,
		};

	public static IPageLink ToPageLink(this MediaPageLinkRow link, MediaType mediaType) => (IPageLink)Create(link.Site, mediaType, link.Path);

	public static IIdentityKey Key(MediaProvider source, MediaType mediaType, string externalId) => (IIdentityKey)Create(source, mediaType, externalId);

	public static MediaPageLinkRow ToPageLinkRow(this IPageLink link, MediaDetailsRow details)
	{
		return new()
		{
			Site = link.Source,
			Path = link.Value,
			Details = details,
		};
	}

	private static IExternalMediaId Create(MediaProvider source, MediaType mediaType, string externalId) =>
		source switch
		{
			MediaProvider.Tmdb => TmdbId.From(externalId, mediaType),
			MediaProvider.Imdb => ImdbId.From(externalId, mediaType),
			MediaProvider.RottenTomatoes => RottenTomatoesId.From(externalId, mediaType),
			MediaProvider.Tvdb => TvdbId.From(externalId, mediaType),
			MediaProvider.Plex => PlexId.From(externalId, mediaType),
			MediaProvider.Mdblist => MdblistId.From(externalId, mediaType),
			MediaProvider.Metacritic => MetacriticId.From(externalId, mediaType),
			MediaProvider.Letterboxd => LetterboxdId.From(externalId, mediaType),
			MediaProvider.Trakt => TraktId.From(externalId, mediaType),
			MediaProvider.Mal => MalId.From(externalId, mediaType),
			MediaProvider.Tvmaze => TvmazeId.From(externalId, mediaType),
			MediaProvider.AniList => AniListId.From(externalId, mediaType),
		};
}
