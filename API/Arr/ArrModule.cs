using API.Arr.Radarr;
using API.Arr.Sonarr;
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
		services.AddHttpClient<RadarrApi>().AddProviderHttp<RadarrOptions>(MediaProvider.Radarr);
		services.AddHttpClient<SonarrApi>().AddProviderHttp<SonarrOptions>(MediaProvider.Sonarr);
		services.AddScoped<ArrApis>();
		services.AddKeyedScoped<IMediaSink, ArrSink>(SinkKind.Arr);
		services.AddKeyedScoped<IMediaSearchProvider, ArrSearch>(SearchProvider.Arr);
		services.AddScoped<IArrConnectionTester, ArrConnectionTester>();
	}
}
