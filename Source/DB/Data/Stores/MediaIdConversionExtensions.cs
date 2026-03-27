using Contracts.Media;
using DB.Data.Entities;

namespace DB.Data.Stores;

internal static class MediaIdConversionExtensions
{
	extension(IMediaId id)
	{
		public MediaIdEntry MapEntity()
		{
			var value = id switch
			{
				ImdbId imdbId => imdbId.Value,
				PlexId plexId => plexId.Value,
				TmdbId tmdbId => tmdbId.Value.ToString(),
				TvdbId tvdbId => tvdbId.Value.ToString(),
			};
			return new MediaIdEntry { Source = id.Source, ExternalId = value };
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
