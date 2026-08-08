using AppHost;
using Shared.Options;

var builder = DistributedApplication.CreateBuilder(args);

builder
	.AddProject<Projects.UI>("blazor-ui")
	.WithHttpEndpoint(port: 6769, name: "http")
	.WithExternalHttpEndpoints()
	.WithEnvironment((ObservabilityOptions o) => o.LogLevel, LogVerbosity.Verbose)
	.WithBrowserLaunch();

builder.Build().Run();
