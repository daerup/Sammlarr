using API.Arr;
using API.Imdb;
using API.Mdblist;
using API.Notifications;
using API.Observability;
using API.Pictures;
using API.Plex;
using API.Tmdb;
using API.Tvdb;
using API.Tvmaze;
using API.Wikidata;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API;

public class ApiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<PlexModule>();
		services.AddModule<TmdbModule>();
		services.AddModule<MdblistModule>();
		services.AddModule<WikidataModule>();
		services.AddModule<TvmazeModule>();
		services.AddModule<ImdbModule>();
		services.AddModule<ArrModule>();
		services.AddModule<PicturesModule>();
		services.AddModule<TvdbModule>();
		services.AddModule<NotificationsApiModule>();
		services.AddModule<InstrumentationModule>();
		services.AddModule<ResilienceModule>();
	}
}
