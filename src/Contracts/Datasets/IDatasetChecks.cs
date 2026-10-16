namespace Contracts.Datasets;

public interface IDatasetChecks
{
	Task RecordAsync(DatasetCheck check, CancellationToken cancellationToken = default);

	Task<Optional<DatasetCheck>> FindAsync(string name, CancellationToken cancellationToken = default);
}

public sealed record DatasetCheck(string Name, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, DatasetOutcome Outcome, Optional<string> Failure);

public enum DatasetOutcome
{
	Loaded,
	Unchanged,
	Failed,
}
