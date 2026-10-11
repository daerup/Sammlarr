namespace Contracts.Housekeeping;

public interface IHousekeepingReports
{
	Task SaveAsync(HousekeepingReport report, CancellationToken cancellationToken = default);

	Task<Optional<HousekeepingReport>> LastAsync(CancellationToken cancellationToken = default);
}

public sealed record HousekeepingReport(
	DateTimeOffset StartedAt,
	Optional<DateTimeOffset> FinishedAt,
	IReadOnlyList<StepCount> Steps,
	Optional<Compaction> File,
	Optional<HousekeepingFailure> Failure
);

public sealed record StepCount(HousekeepingStep Step, int Removed);

public sealed record HousekeepingFailure(Optional<HousekeepingStep> Step, string Reason);
