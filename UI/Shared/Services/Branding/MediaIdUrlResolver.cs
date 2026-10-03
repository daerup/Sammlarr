using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Branding;

public static class ExternalMediaIdUrlResolver
{
	public static string? Resolve(IExternalMediaId id) =>
		id.Source switch
		{
			MediaProvider.Tmdb => id.Type switch
			{
				MediaType.Movie => $"https://www.themoviedb.org/movie/{id.Value}",
				MediaType.Show => $"https://www.themoviedb.org/tv/{id.Value}",
				MediaType.Unknown => $"https://www.themoviedb.org/person/{id.Value}",
			},
			MediaProvider.Imdb => $"https://www.imdb.com/title/{id.Value}",
			MediaProvider.Tvdb => id.Type switch
			{
				MediaType.Movie => $"https://thetvdb.com/dereferrer/movie/{id.Value}",
				MediaType.Show => $"https://thetvdb.com/dereferrer/series/{id.Value}",
				_ => null,
			},
			MediaProvider.Plex => $"https://app.plex.tv/desktop/#!/provider/tv.plex.provider.discover/details?key=%2Flibrary%2Fmetadata%2F{id.Value}",
			MediaProvider.Mdblist => id.Type switch
			{
				MediaType.Movie => $"https://mdblist.com/movie/{id.Value}",
				MediaType.Show => $"https://mdblist.com/show/{id.Value}",
				_ => null,
			},
		};
}
