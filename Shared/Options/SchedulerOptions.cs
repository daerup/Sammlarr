namespace Shared.Options;

public record SchedulerOptions : IOptionsSection
{
	public TimeSpan Grace { get; set; } = TimeSpan.FromMinutes(2);

	public TimeSpan MaxWait { get; set; } = TimeSpan.FromMinutes(30);

	public static string SectionName => "Sammlarr:Scheduler";
}
