using Sammlarr.AppHost;
using Shared.Options;

var builder = DistributedApplication.CreateBuilder(args);

builder
	.AddProject<Projects.UI>("blazor-ui")
	.WithExternalHttpEndpoints()
	.WithEnvironment((TelemetryOptions o) => o.Exporter, TelemetryExporter.Auto)
	.WithEnvironment((TelemetryOptions o) => o.Verbosity, TelemetryVerbosity.Verbose);

builder.Build().Run();
