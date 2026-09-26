using Contracts.Jobs;
using Domain.Identity;
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
			return await RearmAsync(context, queued, job, cancellationToken);
		}

		var row = new JobRow
		{
			Id = Guid.NewGuid(),
			OwnerId = job.Owner.Value,
			Kind = job.Request.Kind,
			Key = job.Request.Key,
			Request = job.Request,
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
				return await RearmAsync(reread, raced, job, cancellationToken);
			}

			throw;
		}
	}

	public async Task<int> RequeueRunningAsync(DateTimeOffset requeuedAt, CancellationToken cancellationToken = default)
	{
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await context
			.Jobs.Where(row => row.State == JobState.Running)
			.ExecuteUpdateAsync(
				setters =>
					setters
						.SetProperty(row => row.State, JobState.Queued)
						.SetProperty(row => row.Attempts, row => row.Attempts + 1)
						.SetProperty(row => row.UpdatedAt, requeuedAt),
				cancellationToken
			);
	}

	public async Task<Optional<RunningJob>> TakeNextAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var candidate = await context
			.Jobs.AsNoTracking()
			.Where(row => row.State == JobState.Queued && row.RunAfter <= now)
			.OrderBy(row => row.RunAfter)
			.Select(row => new
			{
				row.Id,
				row.Request,
				row.OwnerId,
			})
			.FirstOrDefaultAsync(cancellationToken);
		if (candidate is null)
		{
			return new None();
		}

		var claimed = candidate.Id;
		var affected = await context
			.Jobs.Where(row => row.Id == claimed && row.State == JobState.Queued)
			.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.State, JobState.Running).SetProperty(row => row.UpdatedAt, now), cancellationToken);
		return affected == 1 ? Some.Of(new RunningJob(JobId.From(claimed), candidate.Request, UserId.From(candidate.OwnerId))) : new None();
	}

	public async Task CompleteAsync(JobId id, DateTimeOffset completedAt, CancellationToken cancellationToken = default) =>
		await SettleAsync(id, JobState.Done, completedAt, cancellationToken);

	public async Task FailAsync(JobId id, DateTimeOffset failedAt, CancellationToken cancellationToken = default) =>
		await SettleAsync(id, JobState.Failed, failedAt, cancellationToken);

	private async Task SettleAsync(JobId id, JobState outcome, DateTimeOffset settledAt, CancellationToken cancellationToken)
	{
		var raw = id.Value;
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await context
			.Jobs.Where(row => row.Id == raw)
			.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.State, outcome).SetProperty(row => row.UpdatedAt, settledAt), cancellationToken);
	}

	private static async Task<JobRow?> FindQueuedAsync(SammlarrDbContext context, QueuedJob job, CancellationToken cancellationToken)
	{
		var owner = job.Owner.Value;
		var kind = job.Request.Kind;
		var key = job.Request.Key;
		return await context.Jobs.FirstOrDefaultAsync(
			row => row.Kind == kind && row.Key == key && row.OwnerId == owner && row.State == JobState.Queued,
			cancellationToken
		);
	}

	private static async Task<EnqueueResult> RearmAsync(SammlarrDbContext context, JobRow queued, QueuedJob job, CancellationToken cancellationToken)
	{
		var ceiling = queued.FirstArmedAt + job.MaxWait;
		queued.RunAfter = job.RunAfter < ceiling ? job.RunAfter : ceiling;
		await context.SaveChangesAsync(cancellationToken);
		return new AlreadyQueued(JobId.From(queued.Id));
	}
}
