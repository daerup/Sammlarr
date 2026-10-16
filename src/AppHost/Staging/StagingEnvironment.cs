using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace AppHost.Staging;

internal static class StagingEnvironment
{
	internal const string Dockerfile = "src/Web/Dockerfile";

	internal static string RepositoryRoot(IDistributedApplicationBuilder builder) => Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));

	[SuppressMessage(
		"Maintainability",
		"CA1506:Avoid excessive class coupling",
		Justification = "the staging composition root: it wires every staging container, project and command"
	)]
	internal static void AddStaging(this IDistributedApplicationBuilder builder)
	{
		var config = StagingConfig.Load(builder);
		var layout = StagingLayout.From(builder.Configuration["StagingRoot"]);
		var scenario = new StagingScenarioChoice(builder.Configuration["StagingScenario"] ?? StagingScenarioChoice.Configured);
		var tmdb = builder.AddParameter("tmdb-api-key", secret: true);

		var radarr = builder.AddStagingArr(ArrContainers.Radarr, config.Ports.Radarr, layout);
		var sonarr = builder.AddStagingArr(ArrContainers.Sonarr, config.Ports.Sonarr, layout);

		var wiremock = builder
			.AddContainer("wiremock", "wiremock/wiremock", "latest")
			.WithContainerName("sammlarr-staging-wiremock")
			.WithLifetime(ContainerLifetime.Persistent)
			.WithHttpEndpoint(port: config.Ports.WireMock, targetPort: 8080, name: "http", isProxied: false)
			.WithHttpHealthCheck("/__admin/health")
			.WithBindMount(Path.Combine(builder.AppHostDirectory, "Staging", "wiremock"), "/home/wiremock/mappings", isReadOnly: true)
			.WithArgs("--global-response-templating", "--disable-banner")
			.OnResourceReady((resource, _, cancellationToken) => ReloadMappingsAsync(resource.GetEndpoint("http"), cancellationToken));

		var catcher = builder
			.AddContainer("catcher", "ghcr.io/tarampampam/webhook-tester", "2")
			.WithHttpEndpoint(port: config.Ports.Catcher, targetPort: 8080, name: "http", isProxied: false)
			.WithBindMount(layout.Catcher, "/data")
			.WithEnvironment("AUTO_CREATE_SESSIONS", "true")
			.WithEnvironment("STORAGE_DRIVER", "fs")
			.WithEnvironment("FS_STORAGE_DIR", "/data");

		var seed = builder
			.AddProject<Projects.Web>("seed", launchProfileName: null)
			.WithArgs("seed-staging")
			.WithEnvironment((SammlarOptions options) => options.ConnectionString, $"Data Source={layout.Database};Pooling=False")
			.WithEnvironment("Sammlarr__DataProtection__KeyRingPath", layout.KeyRing)
			.WithAppSettings(builder.Configuration)
			.WithConfiguration(builder.Configuration.GetSection(StagingConfig.PersonasSection))
			.WithConfiguration(builder.Configuration.GetSection(StagingConfig.SeedSection))
			.WithEnvironment(context => context.EnvironmentVariables["Staging__Scenario"] = scenario.Value)
			.WithEnvironment("Staging__TmdbKey", tmdb)
			.WithEnvironment("Staging__ArrApiKey", ArrContainers.ApiKey)
			.WithEnvironment("Staging__IndexerUrl", "http://wiremock:8080/newznab")
			.WithEnvironment("Staging__CatcherUrl", $"http://catcher:8080/{config.CatcherSession}")
			.WithArrEnvironment(ArrContainers.Radarr, radarr, layout)
			.WithArrEnvironment(ArrContainers.Sonarr, sonarr, layout)
			.WaitFor(radarr)
			.WaitFor(sonarr)
			.WaitFor(wiremock);
		if (builder.Configuration["Parameters:mdblist-api-key"] is { Length: > 0 } mdblist)
		{
			seed.WithEnvironment("Staging__MdblistKey", mdblist);
		}

		if (builder.Configuration["Parameters:mal-client-id"] is { Length: > 0 } mal)
		{
			seed.WithEnvironment("Staging__MalClientId", mal);
		}

		if (builder.Configuration["Parameters:trakt-client-id"] is { Length: > 0 } trakt)
		{
			seed.WithEnvironment("Staging__TraktClientId", trakt);
		}

		var sammlarr = builder
			.AddDockerfile("sammlarr", RepositoryRoot(builder), Dockerfile)
			.WithHttpEndpoint(port: config.Ports.Sammlarr, targetPort: 6769, name: "http", isProxied: false)
			.WithBindMount(layout.Sammlarr, "/data")
			.WithAppSettings(builder.Configuration)
			.WithEnvironment((PushoverOptions options) => options.BaseUrl, $"http://catcher:8080/{config.CatcherSession}/pushover/1")
			.WithEnvironment("TZ", "Europe/Zurich")
			.WithEnvironment("HTTP_PROXY", config.Network.DeadProxy)
			.WithEnvironment("HTTPS_PROXY", config.Network.DeadProxy)
			.WithEnvironment("NO_PROXY", string.Join(',', config.Network.Reachable))
			.WithOtlpExporter()
			.WithHttpHealthCheck("/health")
			.WaitFor(radarr)
			.WaitFor(sonarr)
			.WaitFor(wiremock)
			.WaitFor(catcher)
			.WaitForCompletion(seed)
			.WithUrls(context =>
			{
				context.Urls.Clear();
				context.Urls.Add(new() { Url = FormattableString.Invariant($"http://{config.Host}:{config.Ports.Sammlarr}"), DisplayText = "Sammlarr" });
			});
		sammlarr.WithStagingCommands(new StagingResources(layout, scenario, radarr, sonarr, seed));
		if (builder.Configuration["LaunchSammlarr"] == "true")
		{
			sammlarr.WithBrowserLaunch(() => FormattableString.Invariant($"http://{config.Host}:{config.Ports.Sammlarr}"));
		}

		if (builder.Configuration["LaunchDashboard"] == "true")
		{
			builder.OpenOnceStarted(builder.Configuration["ASPNETCORE_URLS"]!.Split(';')[0]);
		}

		Label(radarr, "Radarr", "/");
		Label(sonarr, "Sonarr", "/");
		Label(wiremock, "WireMock journal", "/__admin/requests");
		Label(catcher, "Catcher", $"/s/{config.CatcherSession}");
		radarr.WithParentRelationship(sammlarr);
		sonarr.WithParentRelationship(sammlarr);
		wiremock.WithParentRelationship(sammlarr);
		catcher.WithParentRelationship(sammlarr);
		seed.WithParentRelationship(sammlarr);

		var stub = Path.Combine(builder.AppHostDirectory, "Staging", "stub.mkv");
		builder.Services.AddHostedService(services => new FakeDownloader(layout.Downloads, stub, services.GetRequiredService<ILogger<FakeDownloader>>()));
	}

	[SuppressMessage("ApiDesign", "RS0030", Justification = "The AppHost has no ForRemote clients; this one call resets the staging WireMock.")]
	private static async Task ReloadMappingsAsync(EndpointReference endpoint, CancellationToken cancellationToken)
	{
		using var http = new HttpClient();
		using var response = await http.PostAsync(new Uri($"{endpoint.Url}/__admin/mappings/reset"), null, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	private static IResourceBuilder<ContainerResource> AddStagingArr(
		this IDistributedApplicationBuilder builder,
		ArrContainers.ArrContainer app,
		int port,
		StagingLayout layout
	)
	{
		return builder.AddPersistentArr(app, port, layout.ConfigOf(app), layout.LibraryOf(app), layout.AnimeLibraryOf(app), layout.DownloadsOf(app));
	}

	private static IResourceBuilder<ProjectResource> WithArrEnvironment(
		this IResourceBuilder<ProjectResource> seed,
		ArrContainers.ArrContainer app,
		IResourceBuilder<ContainerResource> container,
		StagingLayout layout
	)
	{
		var section = $"Staging__{app.DisplayName}";
		var address = FormattableString.Invariant($"http://{app.Name}:{app.Port}");
		return seed.WithEnvironment($"{section}__Url", container.GetEndpoint("http"))
			.WithEnvironment($"{section}__Address", address)
			.WithEnvironment($"{section}__RootFolder", app.Library)
			.WithEnvironment($"{section}__AnimeRootFolder", app.AnimeLibrary)
			.WithEnvironment($"{section}__Library", layout.LibraryOf(app));
	}

	private static IResourceBuilder<T> WithAppSettings<T>(this IResourceBuilder<T> resource, IConfiguration configuration)
		where T : IResourceWithEnvironment
	{
		foreach (var section in StagingConfig.AppSections)
		{
			resource.WithConfiguration(configuration.GetSection(section));
		}

		return resource;
	}

	private static void Label(IResourceBuilder<ContainerResource> container, string name, string path)
	{
		container.WithUrls(context =>
		{
			var endpoint = context.Urls.First();
			context.Urls.Clear();
			context.Urls.Add(new() { Url = $"{endpoint.Url.TrimEnd('/')}{path}", DisplayText = name });
		});
	}
}
