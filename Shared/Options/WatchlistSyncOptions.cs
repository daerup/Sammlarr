namespace Shared.Options;

public record WatchlistSyncOptions : IOptionsSection
{
	public TimeSpan DrainBudget { get; init; } = TimeSpan.FromSeconds(30);
	public static string SectionName => "Sammlarr:Watchlist";
}
