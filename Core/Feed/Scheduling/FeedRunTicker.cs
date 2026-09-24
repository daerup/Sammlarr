using Contracts.Identity;
using Contracts.Jobs;
using Domain.Jobs;
using Microsoft.Extensions.DependencyInjection;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;

namespace Core.Feed.Scheduling;

internal sealed class FeedRunTicker(IServiceScopeFactory scopes)
{
	internal const string FunctionName = "RunFeed";

	[TickerFunction(FunctionName)]
	public async Task RunAsync(TickerFunctionContext context, CancellationToken cancellationToken)
	{
		var scheduled = await TickerRequestProvider.GetRequestAsync<ScheduledFeed>(context, cancellationToken);
		await using var scope = scopes.CreateAsyncScope();
		using var _ = await scope.ServiceProvider.GetRequiredService<IUserScope>().AsAsync(scheduled.OwnerId, cancellationToken);
		await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync(new RunFeed(scheduled.FeedId, scheduled.Trigger), cancellationToken);
	}
}
