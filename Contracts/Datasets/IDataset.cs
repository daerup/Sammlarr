namespace Contracts.Datasets;

public interface IDataset<TRow>
{
	string Name { get; }

	Task<Result<Optional<DatasetFile<TRow>>>> DownloadAsync(Optional<string> etag, CancellationToken cancellationToken = default);
}

public sealed record DatasetFile<TRow>(Optional<string> ETag, IAsyncEnumerable<TRow> Rows);
