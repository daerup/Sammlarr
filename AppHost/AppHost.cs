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
	builder.AddDockerComposeEnvironment("sammlarr").WithDashboard(enabled: false);

#pragma warning disable ASPIRECOMPUTE003
	var registry = builder.AddContainerRegistry("ghcr", "ghcr.io", "daerup/sammlarr");
#pragma warning restore ASPIRECOMPUTE003

	var ui = builder
		.AddDockerfile("blazor-ui", "..", "Web/Dockerfile")
		.WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
		.WithExternalHttpEndpoints()
		.WithHttpHealthCheck("/health")
		.WithEnvironment("Sammlarr__ConnectionString", "Data Source=/data/sammlarr.db")
		.WithEnvironment("Sammlarr__DataProtection__KeyRingPath", "/data/dp-keys")
		.WithVolume("sammlarr-data", "/data");

#pragma warning disable ASPIRECOMPUTE003
	ui.WithContainerRegistry(registry);
#pragma warning restore ASPIRECOMPUTE003
}

builder.Build().Run();
