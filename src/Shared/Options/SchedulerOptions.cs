namespace Shared.Options;

public record SchedulerOptions : IOptionsSection
{
	public TimeSpan Grace { get; set; } = TimeSpan.FromMinutes(2);

	public TimeSpan MaxWait { get; set; } = TimeSpan.FromMinutes(30);

	public TimeSpan FillDetailsGrace { get; set; } = TimeSpan.FromMinutes(5);

	public TimeSpan FillDetailsMaxWait { get; set; } = TimeSpan.FromMinutes(30);

	public static string SectionName => "Sammlarr:Scheduler";
}
