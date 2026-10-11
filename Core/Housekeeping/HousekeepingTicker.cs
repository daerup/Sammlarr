using Microsoft.Extensions.DependencyInjection;
using TickerQ.Utilities.Base;

namespace Core.Housekeeping;

internal sealed class HousekeepingTicker(IServiceScopeFactory scopes)
{
	internal const string FunctionName = "Housekeeping";

	[TickerFunction(FunctionName)]
	public async Task RunAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<HousekeepingPass>().RunAsync(cancellationToken);
	}
}
