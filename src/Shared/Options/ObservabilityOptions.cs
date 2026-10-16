namespace Shared.Options;

public enum LogVerbosity
{
	Quiet,
	Standard,
	Verbose,
}

public record ObservabilityOptions : IOptionsSection
{
	public LogVerbosity LogLevel { get; set; } = LogVerbosity.Standard;
	public static string SectionName => "Sammlarr:Observability";
}
