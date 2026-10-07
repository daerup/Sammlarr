namespace Contracts.Datasets;

public interface IDatasetLoads
{
	Task<Optional<DatasetLoad>> FindAsync(string name, CancellationToken cancellationToken = default);

	Task RecordAsync(DatasetLoad load, CancellationToken cancellationToken = default);
}

public sealed record DatasetLoad(string Name, DateTimeOffset LoadedAt, Optional<string> ETag, int Rows);
