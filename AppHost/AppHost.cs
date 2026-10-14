using AppHost;
using AppHost.Staging;
using Shared.Options;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsRunMode && builder.Configuration["Staging"] == "true")
{
	builder.AddStaging();
}
else if (builder.ExecutionContext.IsRunMode)
{
	var ui = builder
		.AddProject<Projects.Web>("blazor-ui")
		.WithHttpEndpoint(port: 6769, name: "http")
		.WithExternalHttpEndpoints()
		.WithHttpHealthCheck("/health")
		.WithEnvironment((ObservabilityOptions o) => o.LogLevel, LogVerbosity.Verbose);

	if (builder.Configuration["LaunchSammlarr"] == "true")
	{
		ui.WithBrowserLaunch();
	}

	if (builder.Configuration["RadarrSonarr"] == "true")
	{
		builder.AddRadarrAndSonarr();
	}
}
else
{
	builder.AddDockerComposeEnvironment("compose").WithDashboard(enabled: false);

	builder
		.AddContainer("sammlarr", "ghcr.io/daerup/sammlarr", builder.Configuration["ImageTag"] ?? "latest")
		.WithHttpEndpoint(port: 6769, targetPort: 6769, name: "http")
		.WithExternalHttpEndpoints()
		.WithVolume("sammlarr-data", "/data")
		.PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");
}

await builder.Build().RunAsync();
