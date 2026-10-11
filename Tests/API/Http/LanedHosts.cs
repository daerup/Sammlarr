using API.Http;
using API.Http.Lanes;
using Contracts.Identity;
using Contracts.Providers;
using Contracts.Usage;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.DI;
using Shared.Options;

namespace API.Tests.Http;

internal static class LanedHosts
{
	internal static PlexOptions Plex(ProviderHttpOptions http)
	{
		return new PlexOptions
		{
			BaseUrl = "https://plex.test",
			DiscoverUrl = "https://discover.plex.test",
			MetadataUrl = "https://metadata.plex.test",
			VodUrl = "https://vod.plex.test",
			Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
			Http = http,
		};
	}

	internal static IHost Host<TOptions>(
		TOptions options,
		RemoteService remote,
		HttpMessageHandler wire,
		IProviderRateLimitReporter? reporter = null,
		ILogger<LaneHandler>? logger = null,
		params string[] clients
	)
		where TOptions : class, IProviderHttpOptions
	{
		return new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(options);
				services.AddSingleton(ICurrentUser.Mock().Object);
				services.AddSingleton(reporter ?? IProviderRateLimitReporter.Mock().Object);
				services.AddSingleton(IFeedRunUsage.Mock().Object);
				Optional.Of(logger).Apply(given => services.AddSingleton(given));
				services.AddModule<HttpModule>();
				foreach (var client in clients.DefaultIfEmpty("plex"))
				{
					services.AddHttpClient(client).AddRemoteHttp<TOptions>(remote).ConfigurePrimaryHttpMessageHandler(() => wire);
				}
			})
			.Build();
	}

	internal static HttpClient Client(this IHost host, string name = "plex") => host.Services.GetRequiredService<IHttpClientFactory>().CreateClient(name);

	internal static RemoteLoad Load(this IHost host, RemoteService remote)
	{
		using var scope = host.Services.CreateScope();
		return scope.ServiceProvider.GetRequiredService<IRemoteCapacity>().Of(remote);
	}
}
