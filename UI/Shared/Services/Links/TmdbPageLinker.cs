using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class TmdbPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title) => ids.OfProvider(MediaProvider.Tmdb).Map(Page);

	private static string Page(IExternalMediaId id)
	{
		return id.Type switch
		{
			MediaType.Movie => $"https://www.themoviedb.org/movie/{id.Value}",
			MediaType.Show => $"https://www.themoviedb.org/tv/{id.Value}",
			MediaType.Unknown => $"https://www.themoviedb.org/person/{id.Value}",
		};
	}
}
