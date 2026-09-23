using Contracts.Jobs;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Jobs;

internal sealed class JobRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IJobRepository
{
	public async Task<EnqueueResult> EnqueueAsync(QueuedJob job, CancellationToken cancellationToken = default)
	{
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		if (await FindQueuedAsync(context, job, cancellationToken) is { } queued)
		{
			return new AlreadyQueued(JobId.From(queued));
		}

		var row = new JobRow
		{
			Id = Guid.NewGuid(),
			OwnerId = job.Owner.Value,
			Kind = job.Kind,
			Key = job.Key,
			State = JobState.Queued,
			RunAfter = job.RunAfter,
			FirstArmedAt = job.RunAfter,
		};
		context.Jobs.Add(row);
		try
		{
			await context.SaveChangesAsync(cancellationToken);
			return new Queued(JobId.From(row.Id));
		}
		catch (DbUpdateException)
		{
			await using var reread = await dbContextFactory.CreateDbContextAsync(cancellationToken);
			if (await FindQueuedAsync(reread, job, cancellationToken) is { } raced)
			{
				return new AlreadyQueued(JobId.From(raced));
			}

			throw;
		}
	}

	private static async Task<Guid?> FindQueuedAsync(SammlarrDbContext context, QueuedJob job, CancellationToken cancellationToken)
	{
		var owner = job.Owner.Value;
		return await context
			.Jobs.AsNoTracking()
			.Where(row => row.Kind == job.Kind && row.Key == job.Key && row.OwnerId == owner && row.State == JobState.Queued)
			.Select(row => (Guid?)row.Id)
			.FirstOrDefaultAsync(cancellationToken);
	}
}
