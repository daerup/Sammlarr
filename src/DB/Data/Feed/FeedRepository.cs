using System.Linq.Expressions;
using Contracts.Feed;
using DB.Data.Feed.Run;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed;

internal sealed class FeedRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IFeedRepository
{
	private static readonly Expression<Func<FeedRow, Domain.Feed.Feed>> project = FeedProjection.Project;
	private static readonly Expression<Func<FeedRunRow, FeedRun>> projectRun = FeedRunProjection.Project;

	private static readonly Expression<Func<FeedRunRow, FeedRun>> projectRunCounts = FeedRunProjection.ProjectWithoutItems;

	public async Task<IReadOnlyList<Domain.Feed.Feed>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Feeds.AsNoTracking().OrderByDescending(l => l.CreatedAt).Select(project).ToListAsync(cancellationToken);
	}

	public async Task<Optional<Domain.Feed.Feed>> FindAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return Optional.Of(await ctx.Feeds.AsNoTracking().Where(l => l.Id == id.Value).Select(project).FirstOrDefaultAsync(cancellationToken));
	}

	public async Task<FeedId> CreateAsync(FeedCreateData data, CancellationToken cancellationToken = default)
	{
		var entity = new FeedRow
		{
			Id = data.Id.Map(id => id.Value).GetValueOr(Guid.Empty),
			Name = data.Name,
			IsEnabled = data.IsEnabled,
			Provider = data.Provider,
			Strategy = data.Strategy,
			Identifier = data.Identifier,
			Filter = data.Filter.ToNullable(),
			Sort = data.Sort.ToNullable(),
			Limit = data.Limit.ToNullable(),
			Trigger = data.Schedule,
			RatingStrategy = data.RatingStrategy.ToNullable(),
		};

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		ctx.Feeds.Add(entity);
		await ctx.SaveChangesAsync(cancellationToken);
		return FeedId.From(entity.Id);
	}

	public async Task UpdateAsync(FeedId id, FeedUpdateData data, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx.Feeds.FindAsync([id.Value], cancellationToken) ?? throw new InvalidOperationException($"Feed with id {id} not found.");

		entity.Name = data.Name;
		entity.IsEnabled = data.IsEnabled;
		entity.Provider = data.Provider;
		entity.Strategy = data.Strategy;
		entity.Identifier = data.Identifier;
		entity.Filter = data.Filter.ToNullable();
		entity.Sort = data.Sort.ToNullable();
		entity.Limit = data.Limit.ToNullable();
		entity.Trigger = data.Schedule;
		entity.RatingStrategy = data.RatingStrategy.ToNullable();
		if (data.Strategy is not Mirror)
		{
			ctx.FeedAbsences.RemoveRange(await ctx.FeedAbsences.Where(row => row.FeedId == id.Value).ToListAsync(cancellationToken));
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx.Feeds.FindAsync([id.Value], cancellationToken);
		if (entity == null)
		{
			return;
		}

		ctx.Feeds.Remove(entity);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public Task<IReadOnlyList<FeedId>> PauseAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default) =>
		SetEnabledAsync(ids, enabled: false, cancellationToken);

	public Task<IReadOnlyList<FeedId>> ResumeAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default) =>
		SetEnabledAsync(ids, enabled: true, cancellationToken);

	private async Task<IReadOnlyList<FeedId>> SetEnabledAsync(IReadOnlyCollection<FeedId> ids, bool enabled, CancellationToken cancellationToken)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var keys = ids.Select(id => id.Value).ToList();
		var feeds = await ctx.Feeds.Where(feed => feed.IsEnabled != enabled && keys.Contains(feed.Id)).ToListAsync(cancellationToken);
		foreach (var feed in feeds)
		{
			feed.IsEnabled = enabled;
		}

		await ctx.SaveChangesAsync(cancellationToken);
		return feeds.Select(feed => FeedId.From(feed.Id)).ToList();
	}

	public async Task<IReadOnlyDictionary<MediaId, int>> GetAbsencesAsync(FeedId feedId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx
			.FeedAbsences.AsNoTracking()
			.Where(row => row.FeedId == feedId.Value)
			.ToDictionaryAsync(row => MediaId.From(row.MediaId), row => row.MissedRuns, cancellationToken);
	}

	public async Task<IReadOnlyList<FeedRun>> GetRunsAsync(RunQuery query, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var runs = ctx.FeedRuns.AsNoTracking();
		runs = query.Feed.Map(feed => runs.Where(row => row.FeedId == feed.Value)).GetValueOr(runs);
		runs = query.Since.Map(since => since.ToUniversalTime()).Map(cutoff => runs.Where(row => row.StartedAt >= cutoff)).GetValueOr(runs);

		var linked = query.MustInclude.Map(run => run.Value).ToNullable();
		var newest = runs.OrderByDescending(row => row.StartedAt).Take(query.Limit).Select(row => row.Id);
		var linkedStart = runs.Where(row => row.Id == linked).Select(row => row.StartedAt);

		return await runs.Where(row => newest.Contains(row.Id) || linkedStart.Any(start => row.StartedAt >= start))
			.OrderByDescending(row => row.StartedAt)
			.AsSplitQuery()
			.Select(query.WithItems ? projectRun : projectRunCounts)
			.ToListAsync(cancellationToken);
	}

	public async Task<Optional<DateTimeOffset>> LastConclusiveRunStartAsync(FeedId feedId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var started = await ctx
			.FeedRuns.AsNoTracking()
			.Where(row => row.FeedId == feedId.Value && row.Conclusiveness == Conclusiveness.Conclusive)
			.OrderByDescending(row => row.StartedAt)
			.Select(row => (DateTimeOffset?)row.StartedAt)
			.FirstOrDefaultAsync(cancellationToken);
		return Optional.Of(started);
	}

	public async Task<IReadOnlyDictionary<FeedId, FeedRunTotals>> GetRunTotalsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var totals = await ctx
			.FeedRuns.AsNoTracking()
			.GroupBy(row => row.FeedId)
			.Select(group => new
			{
				FeedId = group.Key,
				TotalRuns = group.Count(),
				FirstRunAt = group.Min(row => row.StartedAt),
				TotalAdded = group.Sum(row => row.Added),
				TotalRemoved = group.Sum(row => row.Removed),
				LatestAt = group.Max(row => row.StartedAt),
			})
			.ToListAsync(cancellationToken);

		if (totals.Count == 0)
		{
			return new Dictionary<FeedId, FeedRunTotals>();
		}

		var latestStamps = totals.Select(total => total.LatestAt).ToList();
		var candidates = await ctx
			.FeedRuns.AsNoTracking()
			.Where(row => latestStamps.Contains(row.StartedAt))
			.AsSplitQuery()
			.Select(projectRun)
			.ToListAsync(cancellationToken);

		var latest = candidates.GroupBy(run => run.FeedId).ToDictionary(group => group.Key, group => group.OrderByDescending(run => run.StartedAt).First());

		return totals
			.Select(total => (Id: FeedId.From(total.FeedId), Total: total))
			.Where(entry => latest.ContainsKey(entry.Id))
			.ToDictionary(
				entry => entry.Id,
				entry => new FeedRunTotals(
					latest[entry.Id],
					new FeedRunHistory
					{
						TotalRuns = entry.Total.TotalRuns,
						FirstRunAt = Some.Of(entry.Total.FirstRunAt),
						TotalAdded = entry.Total.TotalAdded,
						TotalRemoved = entry.Total.TotalRemoved,
					}
				)
			);
	}

	public async Task StartRunAsync(FeedRun run, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var row = new FeedRunRow
		{
			Id = run.Id.Value,
			FeedId = run.FeedId.Value,
			StartedAt = run.StartedAt,
		};
		Fill(row, run);
		ctx.FeedRuns.Add(row);

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task CompleteRunAsync(FeedRun run, Optional<AbsenceChanges> absences, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var row = await ctx
			.FeedRuns.Include(r => r.Items)
			.Include(r => r.Calls)
			.Include(r => r.Times)
			.AsSplitQuery()
			.FirstAsync(r => r.Id == run.Id.Value, cancellationToken);
		row.Items.Clear();
		row.Calls.Clear();
		row.Times.Clear();
		Fill(row, run);
		await absences.Apply(changes => CountAbsencesAsync(ctx, run.FeedId, changes, cancellationToken));

		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static async Task CountAbsencesAsync(SammlarrDbContext ctx, FeedId feedId, AbsenceChanges changes, CancellationToken cancellationToken)
	{
		var held = await ctx.FeedAbsences.Where(row => row.FeedId == feedId.Value).ToDictionaryAsync(row => MediaId.From(row.MediaId), cancellationToken);
		ctx.FeedAbsences.RemoveRange(held.Where(entry => !changes.Missed.Contains(entry.Key)).Select(entry => entry.Value));
		foreach (var missed in changes.Missed)
		{
			if (held.TryGetValue(missed, out var row))
			{
				row.MissedRuns++;
			}
			else
			{
				ctx.FeedAbsences.Add(
					new FeedAbsenceRow
					{
						FeedId = feedId.Value,
						MediaId = missed.Value,
						MissedRuns = 1,
					}
				);
			}
		}
	}

	private static void Fill(FeedRunRow row, FeedRun run)
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
		row.UnmatchedNotFound = run.Counts.UnmatchedNotFound;
		row.UnmatchedCouldNotLookUp = run.Counts.UnmatchedCouldNotLookUp;
		row.UnmatchedConflict = run.Counts.UnmatchedConflict;
		row.Removed = run.Counts.Removed;
		row.Known = run.Counts.Known;
		row.NotFoundEarlier = run.Counts.NotFoundEarlier;
		row.SourceFailure = run.SourceFailure.ToNullable();
		row.Items.AddRange(run.Items.Select(item => new FeedRunItemRow { MediaId = item.MediaId.Value, State = item.State }));
		row.Calls.AddRange(
			run.Calls.Select(call => new FeedRunCallsRow
			{
				Service = call.Service,
				Outcome = call.Outcome,
				Calls = call.Calls,
			})
		);
		row.Times.AddRange(
			run.Times.Select(time => new FeedRunTimesRow
			{
				Service = time.Service,
				UpToMs = time.UpToMs,
				Attempts = time.Attempts,
			})
		);
	}
}
