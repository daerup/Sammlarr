using Microsoft.Extensions.DependencyInjection;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;

namespace Core.Datasets;

internal sealed record DatasetRequest(string Name);

internal sealed class DatasetTicker(IServiceScopeFactory scopes)
{
	internal const string FunctionName = "LoadDataset";

	[TickerFunction(FunctionName)]
	public async Task RunAsync(TickerFunctionContext context, CancellationToken cancellationToken)
	{
		var request = await TickerRequestProvider.GetRequestAsync<DatasetRequest>(context, cancellationToken);
		await using var scope = scopes.CreateAsyncScope();
		foreach (var loader in scope.ServiceProvider.GetServices<IDatasetLoader>().Where(loader => loader.Name == request.Name))
		{
			await loader.LoadAsync(cancellationToken);
		}
	}
}
