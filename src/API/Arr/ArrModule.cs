using API.Arr.Radarr;
using API.Arr.Sonarr;
using API.Http;
using Contracts.Settings;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Arr;

internal sealed class ArrModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.ForRemote<RadarrOptions>(RemoteService.Radarr).Client<RadarrApi>();
		services.ForRemote<SonarrOptions>(RemoteService.Sonarr).Client<SonarrApi>();
		services.ForService(new Domain.Providers.Arr()).Sink<ArrSink>(SinkKind.Arr).Search<ArrSearch>(SearchProvider.Arr);
		services.AddScoped<ArrApis>();
		services.AddScoped<IArrConnectionTester, ArrConnectionTester>();
	}
}
