namespace Shared.Options;

public enum TelemetryVerbosity
{
	Quiet,
	Normal,
	Verbose,
}

public record TelemetryOptions : IOptionsSection
{
	public TelemetryVerbosity Verbosity { get; set; } = TelemetryVerbosity.Quiet;
	public static string SectionName => "Sammlarr:Telemetry";
}
