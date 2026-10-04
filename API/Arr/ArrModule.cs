using API.Http;
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
		services.AddKeyedScoped<IMediaSink, ArrSink>(SinkKind.Arr);
	}
}
