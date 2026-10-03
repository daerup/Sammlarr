using Contracts.Jobs;
using Domain.Identity;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Jobs;

internal sealed class JobRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IJobRepository
{
	public async Task<EnqueueResult> EnqueueAsync(QueuedJob job, DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		if (await FindPendingAsync(context, job, cancellationToken) is { } pending)
		{
			return await RearmAsync(context, pending, job, now, cancellationToken);
		}

		var row = new JobRow
		{
			Id = Guid.NewGuid(),
			OwnerId = job.Owner.Value,
			Kind = job.Request.Kind,
			Key = job.Request.Key,
			Request = job.Request,
			State = StateFor(job.RunAfter, now),
			RunAfter = job.RunAfter,
			Deadline = job.RunAfter + job.MaxWait,
		};
		context.Jobs.Add(row);
		try
		{
			await context.SaveChangesAsync(cancellationToken);
			return new Queued(JobId.From(row.Id), row.State);
		}
		catch (DbUpdateException)
		{
			await using var reread = await dbContextFactory.CreateDbContextAsync(cancellationToken);
			if (await FindPendingAsync(reread, job, cancellationToken) is { } raced)
			{
				return await RearmAsync(reread, raced, job, now, cancellationToken);
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
			.Where(row => row.State == JobState.Queued)
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

	public async Task<Optional<DateTimeOffset>> NextWaitingAsync(CancellationToken cancellationToken = default)
	{
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var due = await context
			.Jobs.AsNoTracking()
			.Where(row => row.State == JobState.Waiting)
			.OrderBy(row => row.RunAfter)
			.Select(row => (DateTimeOffset?)row.RunAfter)
			.FirstOrDefaultAsync(cancellationToken);
		return Optional.Of(due);
	}

	public async Task<int> ReleaseDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await context
			.Jobs.Where(row => row.State == JobState.Waiting && row.RunAfter <= now)
			.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.State, JobState.Queued).SetProperty(row => row.UpdatedAt, now), cancellationToken);
	}

	public async Task<IReadOnlyList<IJobRequest>> QueuedAsync(UserId owner, JobKind kind, CancellationToken cancellationToken = default)
	{
		var raw = owner.Value;
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await context
			.Jobs.AsNoTracking()
			.Where(row => row.OwnerId == raw && row.Kind == kind && row.State == JobState.Queued)
			.Select(row => row.Request)
			.ToListAsync(cancellationToken);
	}

	public async Task<int> QueueWaitingAsync(UserId owner, JobKind kind, DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		var raw = owner.Value;
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await context
			.Jobs.Where(row => row.OwnerId == raw && row.Kind == kind && row.State == JobState.Waiting)
			.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.State, JobState.Queued).SetProperty(row => row.UpdatedAt, now), cancellationToken);
	}

	public async Task<Optional<IJobRequest>> CancelAsync(JobId id, UserId owner, DateTimeOffset cancelledAt, CancellationToken cancellationToken = default)
	{
		var raw = id.Value;
		var ownerId = owner.Value;
		await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var pending = context.Jobs.Where(row => row.Id == raw && row.OwnerId == ownerId && (row.State == JobState.Waiting || row.State == JobState.Queued));
		var request = await pending.AsNoTracking().Select(row => row.Request).FirstOrDefaultAsync(cancellationToken);
		if (request is null)
		{
			return new None();
		}

		var affected = await pending.ExecuteUpdateAsync(
			setters => setters.SetProperty(row => row.State, JobState.Cancelled).SetProperty(row => row.UpdatedAt, cancelledAt),
			cancellationToken
		);
		return affected == 1 ? Some.Of(request) : new None();
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

	private static JobState StateFor(DateTimeOffset runAfter, DateTimeOffset now) => runAfter <= now ? JobState.Queued : JobState.Waiting;

	private static DateTimeOffset Earliest(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;

	private static async Task<JobRow?> FindPendingAsync(SammlarrDbContext context, QueuedJob job, CancellationToken cancellationToken)
	{
		var owner = job.Owner.Value;
		var kind = job.Request.Kind;
		var key = job.Request.Key;
		var running = job.SkipWhileRunning;
		return await context.Jobs.FirstOrDefaultAsync(
			row =>
				row.Kind == kind
				&& row.Key == key
				&& row.OwnerId == owner
				&& (row.State == JobState.Waiting || row.State == JobState.Queued || (running && row.State == JobState.Running)),
			cancellationToken
		);
	}

	private static async Task<EnqueueResult> RearmAsync(
		SammlarrDbContext context,
		JobRow pending,
		QueuedJob job,
		DateTimeOffset now,
		CancellationToken cancellationToken
	)
	{
		if (pending.State is JobState.Running)
		{
			return new AlreadyQueued(JobId.From(pending.Id), JobState.Running);
		}

		pending.Deadline = Earliest(pending.Deadline, job.RunAfter + job.MaxWait);
		pending.RunAfter = Earliest(job.RunAfter, pending.Deadline);
		pending.State = StateFor(pending.RunAfter, now);
		await context.SaveChangesAsync(cancellationToken);
		return new AlreadyQueued(JobId.From(pending.Id), pending.State);
	}
}
