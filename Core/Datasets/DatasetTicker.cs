using Contracts.Datasets;
using Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Entities;

namespace Core.Datasets;

internal sealed record DatasetRequest(string Name);

internal sealed class DatasetTicker(IServiceScopeFactory scopes, TaskActivity activity)
{
	internal const string FunctionName = "LoadDataset";

	[TickerFunction(FunctionName)]
	public async Task RunAsync(TickerFunctionContext context, CancellationToken cancellationToken)
	{
		var request = await TickerRequestProvider.GetRequestAsync<DatasetRequest>(context, cancellationToken);
		await LoadAsync(request.Name, cancellationToken);
	}

	internal static TimeTickerEntity Once(string name, DateTimeOffset at)
	{
		return new TimeTickerEntity
		{
			Function = FunctionName,
			ExecutionTime = at.UtcDateTime,
			Request = TickerHelper.CreateTickerRequest(new DatasetRequest(name)),
		};
	}

	internal async Task LoadAsync(string name, CancellationToken cancellationToken = default)
	{
		using var running = activity.Begin(name);
		await using var scope = scopes.CreateAsyncScope();
		var checks = scope.ServiceProvider.GetRequiredService<IDatasetChecks>();
		foreach (var loader in scope.ServiceProvider.GetServices<IDatasetLoader>().Where(loader => loader.Name == name))
		{
			await checks.RecordAsync(await loader.LoadAsync(cancellationToken), cancellationToken);
		}
	}
}
