namespace Shared.Options;

public record HousekeepingOptions : IOptionsSection
{
	public int FreshDays { get; set; } = 14;

	public int DetailsUnusedDays { get; set; } = 180;

	public int IdsUnusedDays { get; set; } = 365;

	public int RunHistoryDays { get; set; } = 90;

	public int UsageDays { get; set; } = 180;

	public TimeSpan FreshFor => TimeSpan.FromDays(FreshDays);

	public static string SectionName => "Sammlarr:Housekeeping";
}
