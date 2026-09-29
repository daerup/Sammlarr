using System.Linq.Expressions;
using Contracts.Feed;
using DB.Data.Feed.Run;
using DB.Data.Watchlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed;

internal sealed class FeedRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IFeedRepository
{
	private static readonly Expression<Func<FeedRow, Domain.Feed.Feed>> project = FeedProjection.Project;
	private static readonly Expression<Func<FeedRunRow, Domain.Feed.Run.FeedRun>> projectRun = FeedRunProjection.Project;

	public async Task<IReadOnlyList<Domain.Feed.Feed>> GetFeedsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Feeds.AsNoTracking().OrderByDescending(l => l.CreatedAt).Select(project).ToListAsync(cancellationToken);
	}

	public async Task<Domain.Feed.Feed?> GetFeedByIdAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Feeds.AsNoTracking().Where(l => l.Id == id.Value).Select(project).FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<FeedId> CreateFeedAsync(FeedCreateData createData, CancellationToken cancellationToken = default)
	{
		var entity = new FeedRow
		{
			Name = createData.Name,
			IsEnabled = createData.IsEnabled,
			Provider = createData.Provider,
			Strategy = createData.Strategy,
			Identifier = createData.Identifier,
			Filter = createData.Filter,
			Sort = createData.Sort,
			Limit = createData.Limit,
			Trigger = createData.Schedule,
		};

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		ctx.Feeds.Add(entity);
		await ctx.SaveChangesAsync(cancellationToken);
		return FeedId.From(entity.Id);
	}

	public async Task UpdateFeedAsync(FeedId id, FeedUpdateData updateData, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx.Feeds.FindAsync([id.Value], cancellationToken);
		if (entity == null)
		{
			throw new InvalidOperationException($"Feed with id {id} not found.");
		}

		entity.Name = updateData.Name;
		entity.IsEnabled = updateData.IsEnabled;
		entity.Provider = updateData.Provider;
		entity.Strategy = updateData.Strategy;
		entity.Identifier = updateData.Identifier;
		entity.Filter = updateData.Filter;
		entity.Sort = updateData.Sort;
		entity.Limit = updateData.Limit;
		entity.Trigger = updateData.Schedule;

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteFeedAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx.Feeds.FindAsync([id.Value], cancellationToken);
		if (entity == null)
		{
			return;
		}

		await ctx
			.Watchlist.Where(w => w.SourceFeedId == id.Value)
			.ExecuteUpdateAsync(
				s =>
					s.SetProperty(w => w.Origin, WatchlistEntryOrigin.Orphaned)
						.SetProperty(w => w.SourceFeedNameSnapshot, entity.Name)
						.SetProperty(w => w.SourceRunId, (Guid?)null),
				cancellationToken
			);

		ctx.Feeds.Remove(entity);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<FeedId>> DisableFeedsAsync(IReadOnlyCollection<FeedProvider> providers, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var feeds = await ctx.Feeds.Where(feed => feed.IsEnabled && providers.Contains(feed.Provider)).ToListAsync(cancellationToken);
		foreach (var feed in feeds)
		{
			feed.IsEnabled = false;
		}

		await ctx.SaveChangesAsync(cancellationToken);
		return feeds.Select(feed => FeedId.From(feed.Id)).ToList();
	}

	public async Task<IReadOnlyList<FeedId>> SetEnabledAsync(IReadOnlyCollection<FeedId> ids, bool enabled, CancellationToken cancellationToken = default)
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
			.Select(projectRun)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<Domain.Feed.Run.FeedRun>> GetRecentRunsAsync(DateTimeOffset since, int take, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var cutoff = since.ToUniversalTime();

		return await ctx
			.FeedRuns.AsNoTracking()
			.Where(row => row.StartedAt >= cutoff)
			.OrderByDescending(row => row.StartedAt)
			.Take(take)
			.Select(projectRun)
			.ToListAsync(cancellationToken);
	}

	public async Task<int> CountRunsSinceAsync(FeedId feedId, DateTimeOffset since, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var cutoff = since.ToUniversalTime();

		return await ctx.FeedRuns.AsNoTracking().CountAsync(row => row.FeedId == feedId.Value && row.StartedAt >= cutoff, cancellationToken);
	}

	public async Task<Optional<Domain.Feed.Run.FeedRun>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var run = await ctx.FeedRuns.AsNoTracking().Where(row => row.Id == runId).Select(projectRun).FirstOrDefaultAsync(cancellationToken);

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

	public async Task<IReadOnlyDictionary<FeedId, FeedOverview>> GetOverviewsAsync(CancellationToken cancellationToken = default)
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
			return new Dictionary<FeedId, FeedOverview>();
		}

		var latestStamps = totals.Select(total => total.LatestAt).ToList();
		var candidates = await ctx.FeedRuns.AsNoTracking().Where(row => latestStamps.Contains(row.StartedAt)).Select(projectRun).ToListAsync(cancellationToken);

		var latest = candidates.GroupBy(run => run.FeedId).ToDictionary(group => group.Key, group => group.OrderByDescending(run => run.StartedAt).First());

		return totals
			.Select(total => (Id: FeedId.From(total.FeedId), Total: total))
			.Where(entry => latest.ContainsKey(entry.Id))
			.ToDictionary(
				entry => entry.Id,
				entry => new FeedOverview(
					latest[entry.Id],
					new FeedRunHistory
					{
						TotalRuns = entry.Total.TotalRuns,
						FirstRunAt = entry.Total.FirstRunAt,
						TotalAdded = entry.Total.TotalAdded,
						TotalRemoved = entry.Total.TotalRemoved,
					}
				)
			);
	}

	public async Task StartRunAsync(Domain.Feed.Run.FeedRun run, CancellationToken cancellationToken = default)
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

	public async Task CompleteRunAsync(Domain.Feed.Run.FeedRun run, CancellationToken cancellationToken = default)
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
