namespace Shared.Options;

public enum TelemetryVerbosity
{
	Quiet,
	Normal,
	Verbose,
}

public enum TelemetryExporter
{
	Auto,
	Console,
}

public record TelemetryOptions : IOptionsSection
{
	public TelemetryVerbosity Verbosity { get; set; } = TelemetryVerbosity.Quiet;
	public TelemetryExporter Exporter { get; set; } = TelemetryExporter.Auto;
	public static string SectionName => "Sammlarr:Telemetry";
}
