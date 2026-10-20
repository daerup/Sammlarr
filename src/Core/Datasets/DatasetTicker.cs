using Contracts.Datasets;
using Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Entities;

namespace Core.Datasets;

internal sealed record DatasetRequest(string Name, DatasetRead Read);

internal sealed class DatasetTicker(IServiceScopeFactory scopes, TaskActivity activity)
{
	internal const string FunctionName = "LoadDataset";

	[TickerFunction(FunctionName)]
	public async Task RunAsync(TickerFunctionContext context, CancellationToken cancellationToken)
	{
		var request = await TickerRequestProvider.GetRequestAsync<DatasetRequest>(context, cancellationToken);
		await LoadAsync(request.Name, request.Read, cancellationToken);
	}

	internal static TimeTickerEntity Once(string name, DateTimeOffset at, DatasetRead read)
	{
		return new TimeTickerEntity
		{
			Function = FunctionName,
			ExecutionTime = at.UtcDateTime,
			Request = TickerHelper.CreateTickerRequest(new DatasetRequest(name, read)),
		};
	}

	internal async Task LoadAsync(string name, DatasetRead read, CancellationToken cancellationToken = default)
	{
		using var running = activity.Begin(name);
		await using var scope = scopes.CreateAsyncScope();
		var checks = scope.ServiceProvider.GetRequiredService<IDatasetChecks>();
		foreach (var loader in scope.ServiceProvider.GetServices<IDatasetLoader>().Where(loader => loader.Name == name))
		{
			await checks.RecordAsync(await loader.LoadAsync(read, cancellationToken), cancellationToken);
		}
	}
}
