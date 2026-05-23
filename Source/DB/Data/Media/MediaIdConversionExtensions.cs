using System.Collections.ObjectModel;
using Contracts.Media;

namespace DB.Data.Media;

internal static class MediaIdConversionExtensions
{
	public static IMediaId ToMediaId(this ExternalMediaIdEntity externalId) => Create(externalId.Source, externalId.MediaType, externalId.Id);

	public static ExternalMediaIdEntity ToExternalId(this IMediaId mediaId, MediaEntity media) =>
		new()
		{
			Source = mediaId.Source,
			MediaType = mediaId.Type,
			Id = mediaId.Value,
			Media = media,
		};

	public static Collection<ExternalMediaIdEntity> ToExternalIds(this IEnumerable<IMediaId> mediaIds, MediaEntity media) =>
		[.. mediaIds.Select(id => id.ToExternalId(media))];

	private static IMediaId Create(MediaProvider source, MediaType mediaType, string externalId) =>
		source switch
		{
			MediaProvider.Tmdb => TmdbId.From(externalId, mediaType),
			MediaProvider.Imdb => ImdbId.From(externalId, mediaType),
			MediaProvider.Tvdb => TvdbId.From(externalId, mediaType),
			MediaProvider.Plex => PlexId.From(externalId, mediaType),
			MediaProvider.Mdblist => MdblistId.From(externalId, mediaType),
		};
}
