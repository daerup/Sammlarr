using AppHost;
using AppHost.Staging;
using Shared.Options;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.Configuration["Staging"] == "true")
{
	builder.AddStaging();
}
else
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

await builder.Build().RunAsync();
