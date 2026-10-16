using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Serilog.Events;
using Shared.Options;

namespace Observability;

internal sealed record ObservabilityContext(bool OtlpEnabled, string Environment, string Version)
{
	private const string OtlpEndpointEnvVar = "OTEL_EXPORTER_OTLP_ENDPOINT";

	[SuppressMessage("ApiDesign", "RS0030", Justification = "The modules call it while they register their services, before the host's configuration is built.")]
	public static ObservabilityContext Resolve()
	{
		var otlpEnabled = ShouldUseOtlp(System.Environment.GetEnvironmentVariable(OtlpEndpointEnvVar));
		var environment =
			System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
			?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
			?? Environments.Development;
		var version = typeof(ObservabilityContext).Assembly.GetName().Version?.ToString() ?? "0.0.0";
		return new ObservabilityContext(otlpEnabled, environment, version);
	}

	internal static bool ShouldUseOtlp(string? otlpEndpoint) => !string.IsNullOrWhiteSpace(otlpEndpoint);

	public static LogEventLevel MapLevel(LogVerbosity level) =>
		level switch
		{
			LogVerbosity.Verbose => LogEventLevel.Debug,
			LogVerbosity.Standard => LogEventLevel.Information,
			LogVerbosity.Quiet => LogEventLevel.Warning,
		};
}
