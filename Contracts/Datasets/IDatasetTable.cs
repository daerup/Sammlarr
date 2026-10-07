namespace Contracts.Datasets;

public interface IDatasetTable<in TRow>
{
	Task<int> ReplaceAsync(IAsyncEnumerable<TRow> rows, CancellationToken cancellationToken = default);
}
