using Contracts.Datasets;
using Contracts.Housekeeping;
using Domain.Usage;
using Microsoft.Extensions.Logging;
using Observability.Usage;

namespace Core.Datasets;

internal enum DatasetRead
{
	WhenChanged,
	Always,
}

internal interface IDatasetLoader
{
	string Name { get; }

	Task<DatasetCheck> LoadAsync(DatasetRead read, CancellationToken cancellationToken = default);
}

internal sealed class DatasetGate : IDisposable
{
	private readonly SemaphoreSlim _oneLoad = new(1, 1);

	public void Dispose() => _oneLoad.Dispose();

	internal async Task<T> OneAtATimeAsync<T>(Func<Task<T>> load, CancellationToken cancellationToken)
	{
		await _oneLoad.WaitAsync(cancellationToken);
		try
		{
			return await load();
		}
		finally
		{
			_oneLoad.Release();
		}
	}
}

internal sealed class DatasetLoader<TRow>(
	IDataset<TRow> dataset,
	IDatasetTable<TRow> table,
	IDatasetLoads loads,
	IDatabaseFile database,
	TimeProvider clock,
	DatasetGate gate,
	ILogger<DatasetLoader<TRow>> logger
) : IDatasetLoader
{
	public string Name => dataset.Name;

	public Task<DatasetCheck> LoadAsync(DatasetRead read, CancellationToken cancellationToken = default)
	{
		return gate.OneAtATimeAsync(() => LoadNowAsync(read, cancellationToken), cancellationToken);
	}

	private async Task<DatasetCheck> LoadNowAsync(DatasetRead read, CancellationToken cancellationToken)
	{
		using var marked = UsageCauses.Mark(UsageCause.Dataset);
		var started = new DatasetCheck(dataset.Name, clock.GetUtcNow(), clock.GetUtcNow(), DatasetOutcome.Unchanged, new None());
		var last = await loads.FindAsync(dataset.Name, cancellationToken);
		var download = await dataset.DownloadAsync(last.When(_ => read is DatasetRead.WhenChanged).Map(load => load.ETag), cancellationToken);
		var check = await download.Match(
			file => file.Match(changed => ReplaceAsync(started, changed, cancellationToken), () => Task.FromResult(started)),
			error => Failed(started, error.Reason)
		);
		return check with { FinishedAt = clock.GetUtcNow() };
	}

	private async Task<DatasetCheck> ReplaceAsync(DatasetCheck check, DatasetFile<TRow> file, CancellationToken cancellationToken)
	{
		try
		{
			var rows = await table.ReplaceAsync(file.Rows, cancellationToken);
			await loads.RecordAsync(new DatasetLoad(dataset.Name, clock.GetUtcNow(), file.ETag, rows), cancellationToken);
			logger.LogInformation("Loaded {Rows} rows of {Dataset}", rows, dataset.Name);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "{Dataset} could not be loaded, so its last table stays", dataset.Name);
			return Failed(check, exception.Message);
		}

		await database.CompactAsync(cancellationToken);
		return check with { Outcome = DatasetOutcome.Loaded };
	}

	private static DatasetCheck Failed(DatasetCheck check, string reason) => check with { Outcome = DatasetOutcome.Failed, Failure = Some.Of(reason) };
}
