using Contracts.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Shared.Options;

namespace DB.Data.Housekeeping;

internal sealed class RunHistoryTrim(IDbContextFactory<SammlarrDbContext> contexts, HousekeepingOptions options) : IHousekeepingStep
{
	public HousekeepingStep Step => HousekeepingStep.RunHistory;

	public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		var cutoff = (now - TimeSpan.FromDays(options.RunHistoryDays)).ToUniversalTime();
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var expired = ctx.FeedRuns.IgnoreQueryFilters().Where(run => run.StartedAt < cutoff).Select(run => run.Id);
		var items = await ctx.FeedRunItems.IgnoreQueryFilters().Where(item => expired.Contains(item.FeedRunId)).ExecuteDeleteAsync(cancellationToken);
		var calls = await ctx.FeedRunCalls.IgnoreQueryFilters().Where(call => expired.Contains(call.FeedRunId)).ExecuteDeleteAsync(cancellationToken);
		var times = await ctx.FeedRunTimes.IgnoreQueryFilters().Where(time => expired.Contains(time.FeedRunId)).ExecuteDeleteAsync(cancellationToken);
		return items + calls + times;
	}
}
