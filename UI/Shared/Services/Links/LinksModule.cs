using Domain.Providers;
using Shared.DI;

namespace UI.Shared.Services.Links;

internal class LinksModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddKeyedSingleton<IExternalPageLinker, TmdbPageLinker>(MediaProvider.Tmdb);
		services.AddKeyedSingleton<IExternalPageLinker, ImdbPageLinker>(MediaProvider.Imdb);
		services.AddKeyedSingleton<IExternalPageLinker, RottenTomatoesPageLinker>(MediaProvider.RottenTomatoes);
		services.AddKeyedSingleton<IExternalPageLinker, TvdbPageLinker>(MediaProvider.Tvdb);
		services.AddKeyedSingleton<IExternalPageLinker, PlexPageLinker>(MediaProvider.Plex);
		services.AddKeyedSingleton<IExternalPageLinker, MdblistPageLinker>(MediaProvider.Mdblist);
		services.AddKeyedSingleton<IExternalPageLinker, MetacriticPageLinker>(MediaProvider.Metacritic);
		services.AddKeyedSingleton<IExternalPageLinker, LetterboxdPageLinker>(MediaProvider.Letterboxd);
		services.AddKeyedSingleton<IExternalPageLinker, TraktPageLinker>(MediaProvider.Trakt);
		services.AddKeyedSingleton<IExternalPageLinker, MalPageLinker>(MediaProvider.Mal);
	}
}
