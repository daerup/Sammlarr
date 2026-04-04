using Contracts.Media;
using DB.Data.Entities;

namespace DB.Data;

internal static class MediaIdConversionExtensions
{
	extension(IMediaId id)
	{
		public MediaIdEntry MapEntity()
		{
			var value = id.Source switch
			{
				MediaProvider.Tmdb => id.ToString(),
				MediaProvider.Imdb => id.ToString(),
				MediaProvider.Tvdb => id.ToString(),
				MediaProvider.Plex => id.ToString(),
			};
			return new MediaIdEntry { Source = id.Source, ExternalId = value! };
		}
	}

	extension(MediaIdEntry id)
	{
		public IMediaId MapValueObject()
		{
			return id.Source switch
			{
				MediaProvider.Tmdb => TmdbId.Parse(id.ExternalId),
				MediaProvider.Imdb => ImdbId.From(id.ExternalId),
				MediaProvider.Tvdb => TvdbId.Parse(id.ExternalId),
				MediaProvider.Plex => PlexId.From(id.ExternalId),
			};
		}
	}
}
