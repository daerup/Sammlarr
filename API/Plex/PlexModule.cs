using System.Net.Http.Headers;
using API.Http;
using Contracts.Auth;
using Contracts.Media.Artwork;
using Contracts.Media.Conversions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Wantlist;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Plex;

internal class PlexModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<PlexApi>();
		// Forwarded so search and enrichment share one PlexApi (and its _byRatingKey cache) per scope.
		services.AddKeyedScoped<IMediaSearchProvider>(SearchProvider.Plex, (provider, _) => provider.GetRequiredService<PlexApi>());
		services.AddKeyedScoped<IMetadataEnricher>(MediaProvider.Plex, (provider, _) => provider.GetRequiredService<PlexApi>());
		services.AddKeyedScoped<IOAuthProvider, PlexOAuthProvider>(AuthProvider.Plex);
		services.AddKeyedScoped<IMediaSink, PlexSink>(SinkKind.Plex);
		services.AddScoped<IExternalIdConverter, PlexIdConverter>();

		services.AddSingleton<PlexUrls>();
		services.AddKeyedSingleton<IArtworkResizer, PlexArtwork>(ArtworkProvider.Plex);
		services.AddHttpClient<PlexClient>(ConfigurePlexClient).AddProviderHttp<PlexOptions>(MediaProvider.Plex);
		services.AddHttpClient(PlexClient.Unauthenticated, ConfigurePlexClient).AddProviderHttp<PlexOptions>(MediaProvider.Plex);
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
