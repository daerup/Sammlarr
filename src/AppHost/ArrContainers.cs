using System.Diagnostics.CodeAnalysis;

namespace AppHost;

internal static class ArrContainers
{
	internal const string ApiKey = "0123456789abcdef0123456789abcdef";

	private const string LinuxServerId = "911";

	internal static readonly ArrContainer Radarr = new("radarr", "Radarr", "lscr.io/linuxserver/radarr", "version-6.4.4.10685", 7878, "/movies", "/anime-movies");

	internal static readonly ArrContainer Sonarr = new("sonarr", "Sonarr", "lscr.io/linuxserver/sonarr", "version-4.0.20.3014", 8989, "/tv", "/anime");

	public static void AddRadarrAndSonarr(this IDistributedApplicationBuilder builder)
	{
		var dataRoot = builder.Configuration["ArrDataRoot"] ?? Path.Combine(Path.GetTempPath(), "sammlarr-arr");
		foreach (var app in new[] { Radarr, Sonarr })
		{
			builder.AddArr(app, Path.Combine(dataRoot, app.Name));
		}
	}

	internal static IResourceBuilder<ContainerResource> AddPersistentArr(
		this IDistributedApplicationBuilder builder,
		ArrContainer app,
		int port,
		string config,
		string library,
		string animeLibrary,
		string downloads
	)
	{
		var prefix = app.Name.ToUpperInvariant();
		return builder
			.AddContainer(app.Name, app.Image, app.Tag)
			.WithContainerName($"sammlarr-staging-{app.Name}")
			.WithLifetime(ContainerLifetime.Persistent)
			.WithHttpEndpoint(port: port, targetPort: app.Port, name: "http", isProxied: false)
			.WithBindMount(config, "/config")
			.WithBindMount(library, app.Library)
			.WithBindMount(animeLibrary, app.AnimeLibrary)
			.WithBindMount(downloads, "/downloads")
			.WithEnvironment($"{prefix}__AUTH__APIKEY", ApiKey)
			.WithEnvironment($"{prefix}__AUTH__METHOD", "External")
			.WithEnvironment($"{prefix}__AUTH__REQUIRED", "DisabledForLocalAddresses")
			.WithEnvironment($"{prefix}__LOG__ANALYTICSENABLED", "false")
			.WithEnvironment($"{prefix}__APP__INSTANCENAME", $"{app.DisplayName}-Staging")
			.WithHttpHealthCheck("/ping");
	}

	private static void AddArr(this IDistributedApplicationBuilder builder, ArrContainer app, string root)
	{
		if (Directory.Exists(root))
		{
			Directory.Delete(root, recursive: true);
		}

		var config = Directory.CreateDirectory(Path.Combine(root, "config")).FullName;
		var library = Directory.CreateDirectory(Path.Combine(root, "library")).FullName;
		var animeLibrary = Directory.CreateDirectory(Path.Combine(root, "anime")).FullName;
		File.Copy(Path.Combine(builder.AppHostDirectory, "arr", app.Name, "config.xml"), Path.Combine(config, "config.xml"));

		builder
			.AddContainer(app.Name, app.Image, app.Tag)
			.WithHttpEndpoint(port: app.Port, targetPort: app.Port, name: "http")
			.WithBindMount(config, "/config")
			.WithBindMount(library, app.Library)
			.WithBindMount(animeLibrary, app.AnimeLibrary)
			.WithEnvironment("PUID", builder.Configuration["PUID"] ?? LinuxServerId)
			.WithEnvironment("PGID", builder.Configuration["PGID"] ?? LinuxServerId)
			.WithHttpHealthCheck("/ping")
			.OnResourceReady((resource, _, cancellationToken) => AddRootFoldersAsync(resource.GetEndpoint("http"), app, cancellationToken));
	}

	[SuppressMessage("ApiDesign", "RS0030", Justification = "The AppHost has no ForRemote clients; this one setup call adds the Arr root folders.")]
	private static async Task AddRootFoldersAsync(EndpointReference endpoint, ArrContainer app, CancellationToken cancellationToken)
	{
		using var http = new HttpClient { BaseAddress = new Uri($"{endpoint.Url}/api/v3/") };
		http.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
		var folders = await http.GetFromJsonAsync<RootFolder[]>("rootfolder", cancellationToken);
		string[] libraries = [app.Library, app.AnimeLibrary];
		foreach (var library in libraries.Except(folders!.Select(folder => folder.Path)))
		{
			using var response = await http.PostAsJsonAsync("rootfolder", new { path = library }, cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}

	internal sealed record ArrContainer(string Name, string DisplayName, string Image, string Tag, int Port, string Library, string AnimeLibrary);

	private sealed record RootFolder(string Path);
}
