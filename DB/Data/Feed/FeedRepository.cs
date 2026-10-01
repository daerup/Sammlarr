using System.Linq.Expressions;
using Contracts.Feed;
using DB.Data.Feed.Run;
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
			Name = data.Name,
			IsEnabled = data.IsEnabled,
			Provider = data.Provider,
			Strategy = data.Strategy,
			Identifier = data.Identifier,
			Filter = data.Filter,
			Sort = data.Sort,
			Limit = data.Limit,
			Trigger = data.Schedule,
		};

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		ctx.Feeds.Add(entity);
		await ctx.SaveChangesAsync(cancellationToken);
		return FeedId.From(entity.Id);
	}

	public async Task UpdateAsync(FeedId id, FeedUpdateData data, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx.Feeds.FindAsync([id.Value], cancellationToken);
		if (entity == null)
		{
			throw new InvalidOperationException($"Feed with id {id} not found.");
		}

		entity.Name = data.Name;
		entity.IsEnabled = data.IsEnabled;
		entity.Provider = data.Provider;
		entity.Strategy = data.Strategy;
		entity.Identifier = data.Identifier;
		entity.Filter = data.Filter;
		entity.Sort = data.Sort;
		entity.Limit = data.Limit;
		entity.Trigger = data.Schedule;

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

	public async Task<IReadOnlyList<Domain.Feed.Run.FeedRun>> GetRunsAsync(RunQuery query, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		IQueryable<FeedRunRow> runs = ctx.FeedRuns.AsNoTracking();
		if (query.Feed is Some<FeedId>(var feedId))
		{
			runs = runs.Where(row => row.FeedId == feedId.Value);
		}
		if (query.Since is Some<DateTimeOffset>(var since))
		{
			var cutoff = since.ToUniversalTime();
			runs = runs.Where(row => row.StartedAt >= cutoff);
		}

		Guid? linked = query.MustInclude is Some<RunId>(var runId) ? runId.Value : null;
		var newest = runs.OrderByDescending(row => row.StartedAt).Take(query.Limit).Select(row => row.Id);
		var linkedStart = runs.Where(row => row.Id == linked).Select(row => row.StartedAt);

		return await runs.Where(row => newest.Contains(row.Id) || linkedStart.Any(start => row.StartedAt >= start))
			.OrderByDescending(row => row.StartedAt)
			.Select(projectRun)
			.ToListAsync(cancellationToken);
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
		var candidates = await ctx.FeedRuns.AsNoTracking().Where(row => latestStamps.Contains(row.StartedAt)).Select(projectRun).ToListAsync(cancellationToken);

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
			Id = run.Id.Value,
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

		var row = await ctx.FeedRuns.Include(r => r.Items).FirstAsync(r => r.Id == run.Id.Value, cancellationToken);
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
		row.Unmatched = run.Counts.Unmatched;
		row.Removed = run.Counts.Removed;
		row.SourceFailure = run.SourceFailure;
		row.Items.AddRange(run.Items.Select(item => new FeedRunItemRow { MediaId = item.MediaId.Value, State = item.State }));
	}
}
