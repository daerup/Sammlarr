using Sammlarr.AppHost;
using Shared.Options;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.UI>("blazor-ui").WithExternalHttpEndpoints().WithEnvironment((ObservabilityOptions o) => o.LogLevel, LogVerbosity.Verbose);

builder.Build().Run();
