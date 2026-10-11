using Contracts.Housekeeping;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;

namespace DB.Data.Housekeeping;

internal sealed class ScheduleEntriesTrim(IDbContextFactory<SammlarrDbContext> contexts) : IHousekeepingStep
{
	private static readonly TimeSpan Kept = TimeSpan.FromDays(30);

	private static readonly TickerStatus[] Settled = [TickerStatus.Done, TickerStatus.DueDone, TickerStatus.Failed, TickerStatus.Cancelled, TickerStatus.Skipped];

	public HousekeepingStep Step => HousekeepingStep.ScheduleEntries;

	public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		var cutoff = (now - Kept).UtcDateTime;
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		return await ctx.Set<CronTickerOccurrenceEntity<CronTickerEntity>>()
			.Where(occurrence => Settled.Contains(occurrence.Status) && occurrence.ExecutionTime < cutoff)
			.ExecuteDeleteAsync(cancellationToken);
	}
}
