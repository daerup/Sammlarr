namespace Contracts.Housekeeping;

public enum HousekeepingStep
{
	RunHistory,
	UsageStats,
	CachedDetails,
	TitleIds,
	SettledJobs,
	ScheduleEntries,
}

public interface IHousekeepingStep
{
	HousekeepingStep Step { get; }

	Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
