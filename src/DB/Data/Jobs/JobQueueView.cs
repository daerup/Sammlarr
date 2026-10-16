using Contracts.Identity;
using Contracts.Jobs;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Jobs;

internal sealed class JobQueueView(IDbContextFactory<SammlarrDbContext> dbContextFactory, ICurrentUser currentUser) : IJobQueueView
{
	public async Task<QueueSnapshot> GetAsync(CancellationToken cancellationToken = default)
	{
		var owner = currentUser.Id.Value;
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var waiting = context.Jobs.AsNoTracking().Where(row => row.State == JobState.Waiting || row.State == JobState.Queued || row.State == JobState.Running);

		var mine = await waiting
			.Where(row => row.OwnerId == owner)
			.OrderBy(row => row.RunAfter)
			.Select(row => new PendingJob(JobId.From(row.Id), row.Request, row.State, row.RunAfter, row.Attempts))
			.ToListAsync(cancellationToken);

		return new QueueSnapshot(mine, await waiting.CountAsync(row => row.OwnerId != owner, cancellationToken));
	}
}
