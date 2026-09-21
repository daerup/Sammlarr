using System.Linq.Expressions;
using Contracts.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.FeedRun;

internal sealed class FeedRunLog(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IFeedRunLog
{
	private static readonly Expression<Func<FeedRunRow, Domain.Feed.Run.FeedRun>> project = row => new Domain.Feed.Run.FeedRun
	{
		Id = row.Id,
		FeedId = FeedId.From(row.FeedId),
		StartedAt = row.StartedAt,
		Duration = TimeSpan.FromMilliseconds(row.DurationMs),
		Strategy = row.Strategy,
		Trigger = row.Trigger,
		Conclusiveness = row.Conclusiveness,
		Counts = new FeedRunCounts
		{
			Total = row.Total,
			Added = row.Added,
			Kept = row.Kept,
			Bypassed = row.Bypassed,
			Filtered = row.Filtered,
			Skipped = row.Skipped,
			Unresolved = row.Unresolved,
			Removed = row.Removed,
		},
		Items = row.Items.Select(item => new FeedRunItem { MediaId = MediaId.From(item.MediaId), State = item.State }).ToList(),
	};

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

	public async Task RecordAsync(FeedRunRecord record, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		ctx.FeedRuns.Add(
			new FeedRunRow
			{
				FeedId = record.FeedId.Value,
				StartedAt = record.StartedAt,
				DurationMs = (long)record.Duration.TotalMilliseconds,
				Strategy = record.Strategy,
				Trigger = record.Trigger,
				Conclusiveness = record.Conclusiveness,
				Total = record.Counts.Total,
				Added = record.Counts.Added,
				Kept = record.Counts.Kept,
				Bypassed = record.Counts.Bypassed,
				Filtered = record.Counts.Filtered,
				Skipped = record.Counts.Skipped,
				Unresolved = record.Counts.Unresolved,
				Removed = record.Counts.Removed,
				Items = [.. record.Items.Select(item => new FeedRunItemRow { MediaId = item.MediaId.Value, State = item.State })],
			}
		);

		await ctx.SaveChangesAsync(cancellationToken);
	}
}
