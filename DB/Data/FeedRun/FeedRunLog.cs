using System.Linq.Expressions;
using Contracts.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.FeedRun;

internal sealed class FeedRunLog(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IFeedRunLog
{
	private static readonly Expression<Func<FeedRunRow, Domain.Feed.Run.FeedRun>> project = FeedRunProjection.Project;

	public async Task<IReadOnlyDictionary<MediaId, int>> GetAbsencesAsync(
		FeedId feedId,
		IReadOnlySet<MediaId> owned,
		int window,
		CancellationToken cancellationToken = default
	)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var recent = await ctx
			.FeedRuns.AsNoTracking()
			.Where(row => row.FeedId == feedId.Value && row.Conclusiveness == Conclusiveness.Conclusive)
			.OrderByDescending(row => row.StartedAt)
			.Take(window)
			.Select(row => row.Items.Select(item => item.MediaId).ToList())
			.ToListAsync(cancellationToken);

		var seen = recent.Select(ids => ids.ToHashSet()).ToList();

		return owned
			.Select(id => (Id: id, Absences: seen.TakeWhile(ids => !ids.Contains(id.Value)).Count()))
			.Where(entry => entry.Absences > 0)
			.ToDictionary(entry => entry.Id, entry => entry.Absences);
	}

	public async Task<IReadOnlyList<Domain.Feed.Run.FeedRun>> GetRunsAsync(FeedId feedId, int take, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		return await ctx
			.FeedRuns.AsNoTracking()
			.Where(row => row.FeedId == feedId.Value)
			.OrderByDescending(row => row.StartedAt)
			.Take(take)
			.Select(project)
			.ToListAsync(cancellationToken);
	}

	public async Task<Optional<Domain.Feed.Run.FeedRun>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var run = await ctx.FeedRuns.AsNoTracking().Where(row => row.Id == runId).Select(project).FirstOrDefaultAsync(cancellationToken);

		return run is null ? new None() : Some.Of(run);
	}

	public async Task<FeedRunHistory> GetHistoryAsync(FeedId feedId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var runs = ctx.FeedRuns.AsNoTracking().Where(row => row.FeedId == feedId.Value);
		if (!await runs.AnyAsync(cancellationToken))
		{
			return FeedRunHistory.None;
		}

		return new FeedRunHistory
		{
			TotalRuns = await runs.CountAsync(cancellationToken),
			FirstRunAt = await runs.MinAsync(row => row.StartedAt, cancellationToken),
			TotalAdded = await runs.SumAsync(row => row.Added, cancellationToken),
			TotalRemoved = await runs.SumAsync(row => row.Removed, cancellationToken),
		};
	}

	public async Task StartAsync(Domain.Feed.Run.FeedRun run, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var row = new FeedRunRow
		{
			Id = run.Id,
			FeedId = run.FeedId.Value,
			StartedAt = run.StartedAt,
		};
		Fill(row, run);
		ctx.FeedRuns.Add(row);

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task CompleteAsync(Domain.Feed.Run.FeedRun run, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var row = await ctx.FeedRuns.Include(r => r.Items).FirstAsync(r => r.Id == run.Id, cancellationToken);
		row.Items.Clear();
		Fill(row, run);

		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static void Fill(FeedRunRow row, Domain.Feed.Run.FeedRun run)
	{
		row.DurationMs = (long)run.Duration.TotalMilliseconds;
		row.Strategy = run.Strategy;
		row.Trigger = run.Trigger;
		row.Conclusiveness = run.Conclusiveness;
		row.Total = run.Counts.Total;
		row.Added = run.Counts.Added;
		row.Kept = run.Counts.Kept;
		row.Bypassed = run.Counts.Bypassed;
		row.Filtered = run.Counts.Filtered;
		row.Skipped = run.Counts.Skipped;
		row.Unresolved = run.Counts.Unresolved;
		row.Removed = run.Counts.Removed;
		row.Items.AddRange(run.Items.Select(item => new FeedRunItemRow { MediaId = item.MediaId.Value, State = item.State }));
	}
}
