using Contracts;
using Contracts.Datasets;
using Microsoft.Extensions.Logging;

namespace Core.Datasets;

internal interface IDatasetLoader
{
	string Name { get; }

	Task<Result<int>> LoadAsync(CancellationToken cancellationToken = default);
}

internal sealed class DatasetLoader<TRow>(
	IDataset<TRow> dataset,
	IDatasetTable<TRow> table,
	IDatasetLoads loads,
	IDateTimeProvider clock,
	ILogger<DatasetLoader<TRow>> logger
) : IDatasetLoader
{
	public string Name => dataset.Name;

	public async Task<Result<int>> LoadAsync(CancellationToken cancellationToken = default)
	{
		var last = await loads.FindAsync(dataset.Name, cancellationToken);
		var download = await dataset.DownloadAsync(last.Map(load => load.ETag), cancellationToken);
		return await download.Map(file => file.Match(changed => ReplaceAsync(changed, cancellationToken), () => Task.FromResult<Result<int>>(Success.Of(0))));
	}

	private async Task<Result<int>> ReplaceAsync(DatasetFile<TRow> file, CancellationToken cancellationToken)
	{
		try
		{
			var rows = await table.ReplaceAsync(file.Rows, cancellationToken);
			await loads.RecordAsync(new DatasetLoad(dataset.Name, clock.Now, file.ETag, rows), cancellationToken);
			logger.LogInformation("Loaded {Rows} rows of {Dataset}", rows, dataset.Name);
			return Success.Of(rows);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "{Dataset} could not be loaded, so its last table stays", dataset.Name);
			return Error.From(exception);
		}
	}
}
