namespace AppHost;

internal static class ArrContainers
{
	private const string ApiKey = "0123456789abcdef0123456789abcdef";

	private static readonly ArrContainer[] Apps =
	[
		new("radarr", "lscr.io/linuxserver/radarr", "6.4.4.10685-ls318", 7878, "/movies"),
		new("sonarr", "lscr.io/linuxserver/sonarr", "4.0.20.3014-ls326", 8989, "/tv"),
	];

	public static void AddRadarrAndSonarr(this IDistributedApplicationBuilder builder)
	{
		var dataRoot = builder.Configuration["ArrDataRoot"] ?? Path.Combine(Path.GetTempPath(), "sammlarr-arr");
		foreach (var app in Apps)
		{
			builder.AddArr(app, Path.Combine(dataRoot, app.Name));
		}
	}

	private static void AddArr(this IDistributedApplicationBuilder builder, ArrContainer app, string root)
	{
		if (Directory.Exists(root))
		{
			Directory.Delete(root, recursive: true);
		}

		var config = Directory.CreateDirectory(Path.Combine(root, "config")).FullName;
		var library = Directory.CreateDirectory(Path.Combine(root, "library")).FullName;
		File.Copy(Path.Combine(builder.AppHostDirectory, "arr", app.Name, "config.xml"), Path.Combine(config, "config.xml"));

		builder
			.AddContainer(app.Name, app.Image, app.Tag)
			.WithHttpEndpoint(port: app.Port, targetPort: app.Port, name: "http")
			.WithBindMount(config, "/config")
			.WithBindMount(library, app.Library)
			.WithHttpHealthCheck("/ping")
			.OnResourceReady((resource, _, cancellationToken) => AddRootFolderAsync(resource.GetEndpoint("http"), app.Library, cancellationToken));
	}

	private static async Task AddRootFolderAsync(EndpointReference endpoint, string library, CancellationToken cancellationToken)
	{
		using var http = new HttpClient { BaseAddress = new Uri($"{endpoint.Url}/api/v3/") };
		http.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
		using var response = await http.PostAsJsonAsync("rootfolder", new { path = library }, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	private sealed record ArrContainer(string Name, string Image, string Tag, int Port, string Library);
}
