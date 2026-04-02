using System.Net.Http.Headers;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Plex;

internal class PlexModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<PlexApi>();
		services.AddKeyedScoped<IMediaSearchService>(MediaProvider.Plex, (sp, _) => sp.GetRequiredService<PlexApi>());
		services.AddKeyedScoped<IOAuthService, PlexOAuthService>(AuthProvider.Plex);
		services.AddKeyedScoped<IWatchlistService, PlexWatchListService>(AuthProvider.Plex);
		services.AddKeyedScoped<IMediaIdConverter, PlexIdConverter>(MediaProvider.Plex);

		services.AddHttpClient<PlexClient>(ConfigurePlexClient);
		services.AddHttpClient(PlexClient.Unauthenticated, ConfigurePlexClient);
	}

	private static void ConfigurePlexClient(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<PlexOptions>();
		var auth = options.Authentication;

		client.BaseAddress = new Uri(options.BaseUrl);
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		client.DefaultRequestHeaders.UserAgent.ParseAdd("Sammlarr/1.0");
		client.DefaultRequestHeaders.Add("X-Plex-Product", auth.ProductName);
		client.DefaultRequestHeaders.Add("X-Plex-Version", "1.0");
		client.DefaultRequestHeaders.Add("X-Plex-Device", "Web");
		client.DefaultRequestHeaders.Add("X-Plex-Device-Name", auth.ProductName);
		client.DefaultRequestHeaders.Add("X-Plex-Platform", "Web");
		client.DefaultRequestHeaders.Add("X-Plex-Container-Size", PlexClient.ContainerSize.ToString());
	}
}
