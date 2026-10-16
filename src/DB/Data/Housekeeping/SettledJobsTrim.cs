using Contracts.Housekeeping;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Housekeeping;

internal sealed class SettledJobsTrim(IDbContextFactory<SammlarrDbContext> contexts) : IHousekeepingStep
{
	private static readonly TimeSpan Kept = TimeSpan.FromDays(30);

	private static readonly JobState[] Settled = [JobState.Done, JobState.Failed, JobState.Cancelled];

	public HousekeepingStep Step => HousekeepingStep.SettledJobs;

	public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		var cutoff = (now - Kept).ToUniversalTime();
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		return await ctx.Jobs.Where(job => Settled.Contains(job.State) && job.UpdatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
	}
}
