using System.Net.Http.Headers;
using API.Http;
using Contracts.Media.Artwork;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Plex;

internal class PlexModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<PlexUrls>();
		services.AddSingleton<IArtworkResizer, PlexArtwork>();
		services
			.ForRemote<PlexOptions>(RemoteService.Plex)
			.Client<PlexClient>(ConfigurePlexClient)
			.Client(PlexClient.Unauthenticated, ConfigurePlexClient)
			.Credential<PlexOAuthProvider>(AuthProvider.Plex)
			.Search<PlexApi>(SearchProvider.Plex)
			.Sink<PlexSink>(SinkKind.Plex)
			.Converter<PlexIdConverter>()
			.Enricher<PlexApi>();
	}

	private static void ConfigurePlexClient(IServiceProvider sp, HttpClient client)
	{
		var plexOptions = sp.GetRequiredService<PlexOptions>();
		var auth = plexOptions.Authentication;

		client.BaseAddress = new Uri(plexOptions.BaseUrl);
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		client.DefaultRequestHeaders.UserAgent.ParseAdd($"{auth.ProductName}/1.0");
		client.DefaultRequestHeaders.Add("X-Plex-Product", auth.ProductName);
		client.DefaultRequestHeaders.Add("X-Plex-Version", "1.0");
		client.DefaultRequestHeaders.Add("X-Plex-Device", "Web");
		client.DefaultRequestHeaders.Add("X-Plex-Device-Name", auth.ProductName);
		client.DefaultRequestHeaders.Add("X-Plex-Platform", "Web");
		client.DefaultRequestHeaders.Add("X-Plex-Container-Size", PlexClient.ContainerSize.ToString());
	}
}
