using API.Http;
using Contracts.Media.Search;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Arr;

internal class ArrModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<RadarrClient>().AddProviderHttp<ArrOptions>(MediaProvider.Radarr);
		services.AddHttpClient<SonarrClient>().AddProviderHttp<ArrOptions>(MediaProvider.Sonarr);
		services.AddScoped<ArrClients>();
		services.AddKeyedScoped<IMediaSink, ArrSink>(SinkKind.Arr);
		services.AddKeyedScoped<IMediaSearchProvider, ArrSearch>(SearchProvider.Arr);
		services.AddScoped<IArrConnectionTester, ArrConnectionTester>();
	}
}
