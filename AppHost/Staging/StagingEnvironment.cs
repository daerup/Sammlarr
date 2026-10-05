using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace AppHost.Staging;

internal static class StagingEnvironment
{
	internal const string Dockerfile = "Web/Dockerfile";

	private const string Host = "staging.localhost";

	private const string CatcherSession = "5a3a7a6e-0000-4000-8000-000000000001";

	private const int SammlarrPort = 6789;

	private const int CatcherPort = 6790;

	private const int WireMockPort = 6791;

	private const string DeadProxy = "http://127.0.0.1:9";

	private static readonly string[] Reachable =
	[
		"api.themoviedb.org",
		"image.tmdb.org",
		"api.mdblist.com",
		"query.wikidata.org",
		"radarr",
		"sonarr",
		"wiremock",
		"catcher",
		"host.docker.internal",
		"aspire.dev.internal",
		"localhost",
		"127.0.0.1",
	];

	internal static string RepositoryRoot(IDistributedApplicationBuilder builder) => Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));

	internal static void AddStaging(this IDistributedApplicationBuilder builder)
	{
		var layout = StagingLayout.From(builder.Configuration["StagingRoot"]);
		var scenario = new StagingScenarioChoice(builder.Configuration["StagingScenario"] ?? StagingScenarioChoice.Configured);
		var tmdb = builder.AddParameter("tmdb-api-key", secret: true);

		var radarr = builder.AddStagingArr(ArrContainers.Radarr, 7879, layout);
		var sonarr = builder.AddStagingArr(ArrContainers.Sonarr, 8990, layout);

		var wiremock = builder
			.AddContainer("wiremock", "wiremock/wiremock", "latest")
			.WithContainerName("sammlarr-staging-wiremock")
			.WithLifetime(ContainerLifetime.Persistent)
			.WithHttpEndpoint(port: WireMockPort, targetPort: 8080, name: "http", isProxied: false)
			.WithHttpHealthCheck("/__admin/health")
			.WithBindMount(Path.Combine(builder.AppHostDirectory, "Staging", "wiremock"), "/home/wiremock/mappings", isReadOnly: true)
			.WithArgs("--global-response-templating", "--disable-banner");

		var catcher = builder
			.AddContainer("catcher", "ghcr.io/tarampampam/webhook-tester", "2")
			.WithHttpEndpoint(port: CatcherPort, targetPort: 8080, name: "http", isProxied: false)
			.WithBindMount(layout.Catcher, "/data")
			.WithEnvironment("AUTO_CREATE_SESSIONS", "true")
			.WithEnvironment("STORAGE_DRIVER", "fs")
			.WithEnvironment("FS_STORAGE_DIR", "/data");

		var seed = builder
			.AddProject<Projects.Web>("seed", launchProfileName: null)
			.WithArgs("seed-staging")
			.WithEnvironment((SammlarOptions options) => options.ConnectionString, $"Data Source={layout.Database};Pooling=False")
			.WithEnvironment("Sammlarr__DataProtection__KeyRingPath", layout.KeyRing)
			.WithEnvironment(context => context.EnvironmentVariables["Staging__Scenario"] = scenario.Value)
			.WithEnvironment("Staging__TmdbKey", tmdb)
			.WithEnvironment("Staging__ArrApiKey", ArrContainers.ApiKey)
			.WithEnvironment("Staging__IndexerUrl", "http://wiremock:8080/newznab")
			.WithEnvironment("Staging__CatcherUrl", $"http://catcher:8080/{CatcherSession}")
			.WithArrEnvironment(ArrContainers.Radarr, radarr, layout)
			.WithArrEnvironment(ArrContainers.Sonarr, sonarr, layout)
			.WaitFor(radarr)
			.WaitFor(sonarr)
			.WaitFor(wiremock);
		if (builder.Configuration["Parameters:mdblist-api-key"] is { Length: > 0 } mdblist)
		{
			seed.WithEnvironment("Staging__MdblistKey", mdblist);
		}

		var sammlarr = builder
			.AddDockerfile("sammlarr", RepositoryRoot(builder), Dockerfile)
			.WithHttpEndpoint(port: SammlarrPort, targetPort: 8080, name: "http", isProxied: false)
			.WithBindMount(layout.Sammlarr, "/data")
			.WithEnvironment((PushoverOptions options) => options.BaseUrl, $"http://catcher:8080/{CatcherSession}/pushover/1")
			.WithEnvironment("HTTP_PROXY", DeadProxy)
			.WithEnvironment("HTTPS_PROXY", DeadProxy)
			.WithEnvironment("NO_PROXY", string.Join(',', Reachable))
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
				context.Urls.Add(new() { Url = $"http://{Host}:{SammlarrPort}", DisplayText = "Sammlarr" });
			});
		foreach (var key in PlexOptions.UrlSettings)
		{
			sammlarr.WithEnvironment($"{PlexOptions.SectionName}__{key}", $"http://wiremock:8080/plex-blocked/{key}");
		}

		sammlarr.WithStagingCommands(new StagingResources(layout, scenario, radarr, sonarr, seed));
		Label(radarr, "Radarr", "/");
		Label(sonarr, "Sonarr", "/");
		Label(wiremock, "WireMock journal", "/__admin/requests");
		Label(catcher, "Catcher", $"/s/{CatcherSession}");
		radarr.WithParentRelationship(sammlarr);
		sonarr.WithParentRelationship(sammlarr);
		wiremock.WithParentRelationship(sammlarr);
		catcher.WithParentRelationship(sammlarr);
		seed.WithParentRelationship(sammlarr);

		var stub = Path.Combine(builder.AppHostDirectory, "Staging", "stub.mkv");
		builder.Services.AddHostedService(services => new FakeDownloader(layout.Downloads, stub, services.GetRequiredService<ILogger<FakeDownloader>>()));
	}

	private static IResourceBuilder<ContainerResource> AddStagingArr(
		this IDistributedApplicationBuilder builder,
		ArrContainers.ArrContainer app,
		int port,
		StagingLayout layout
	)
	{
		return builder.AddPersistentArr(app, port, layout.ConfigOf(app), layout.LibraryOf(app), layout.DownloadsOf(app));
	}

	private static IResourceBuilder<ProjectResource> WithArrEnvironment(
		this IResourceBuilder<ProjectResource> seed,
		ArrContainers.ArrContainer app,
		IResourceBuilder<ContainerResource> container,
		StagingLayout layout
	)
	{
		var section = $"Staging__{app.DisplayName}";
		var address = $"http://{app.Name}:{app.Port}";
		return seed.WithEnvironment($"{section}__Url", container.GetEndpoint("http"))
			.WithEnvironment($"{section}__Address", address)
			.WithEnvironment($"{section}__RootFolder", app.Library)
			.WithEnvironment($"{section}__Library", layout.LibraryOf(app));
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
