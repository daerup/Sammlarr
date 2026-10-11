using Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Entities;

namespace Core.Housekeeping;

internal sealed class HousekeepingTicker(IServiceScopeFactory scopes, TaskActivity activity)
{
	internal const string FunctionName = "Housekeeping";

	[TickerFunction(FunctionName)]
	public async Task RunAsync(CancellationToken cancellationToken)
	{
		using var running = activity.Begin(FunctionName);
		await using var scope = scopes.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<HousekeepingPass>().RunAsync(cancellationToken);
	}

	internal static TimeTickerEntity Once(DateTimeOffset at) => new() { Function = FunctionName, ExecutionTime = at.UtcDateTime };
}
