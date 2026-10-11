using Contracts.Usage;
using Domain.Feed;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Usage;

internal sealed class UsageStore(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IUsageStore
{
	public async Task AddAsync(UsageRows rows, CancellationToken cancellationToken = default)
	{
		var hours = rows.Calls.Select(row => row.Hour).Concat(rows.Attempts.Select(row => row.Hour)).ToList();
		var from = hours.Min().ToUniversalTime();
		var to = hours.Max().ToUniversalTime();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var calls = await ctx.RemoteUsage.Where(row => row.Hour >= from && row.Hour <= to).ToListAsync(cancellationToken);
		var attempts = await ctx.RemoteAttempts.Include(row => row.Buckets).Where(row => row.Hour >= from && row.Hour <= to).ToListAsync(cancellationToken);
		foreach (var call in rows.Calls)
		{
			var row = calls.Find(existing => Holds(existing, call)) ?? Added(ctx, calls, call);
			row.Calls += call.Calls;
		}

		foreach (var attempt in rows.Attempts)
		{
			var row = attempts.Find(existing => Holds(existing, attempt)) ?? Added(ctx, attempts, attempt);
			row.Attempts += attempt.Attempts;
			foreach (var (upToMs, count) in attempt.Buckets)
			{
				var bucket = row.Buckets.Find(existing => existing.UpToMs == upToMs) ?? Added(row.Buckets, upToMs);
				bucket.Count += count;
			}
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task<UsageRows> ReadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
	{
		var start = from.ToUniversalTime();
		var end = to.ToUniversalTime();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var calls = await ctx.RemoteUsage.AsNoTracking().Where(row => row.Hour >= start && row.Hour < end).ToListAsync(cancellationToken);
		var attempts = await ctx
			.RemoteAttempts.AsNoTracking()
			.Include(row => row.Buckets)
			.Where(row => row.Hour >= start && row.Hour < end)
			.ToListAsync(cancellationToken);
		return new UsageRows([.. calls.Select(Calls)], [.. attempts.Select(Attempts)]);
	}

	public async Task PruneAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
	{
		var cutOff = before.ToUniversalTime();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.RemoteUsage.Where(row => row.Hour < cutOff).ExecuteDeleteAsync(cancellationToken);
		await ctx.RemoteAttempts.Where(row => row.Hour < cutOff).ExecuteDeleteAsync(cancellationToken);
		await ctx.DeletedFeeds.IgnoreQueryFilters().Where(row => row.DeletedAt < cutOff).ExecuteDeleteAsync(cancellationToken);
	}

	private static bool Holds(RemoteUsageRow row, UsageCalls calls)
	{
		return row.Service == calls.Service
			&& row.Hour == calls.Hour
			&& row.Cause == calls.Cause
			&& row.Outcome == calls.Outcome
			&& row.FeedId == FeedOf(calls.Feed);
	}

	private static bool Holds(RemoteAttemptRow row, UsageAttempts attempts)
	{
		return row.Service == attempts.Service && row.Hour == attempts.Hour && row.Cause == attempts.Cause && row.FeedId == FeedOf(attempts.Feed);
	}

	private static RemoteUsageRow Added(SammlarrDbContext ctx, List<RemoteUsageRow> rows, UsageCalls calls)
	{
		var row = new RemoteUsageRow
		{
			Service = calls.Service,
			Hour = calls.Hour,
			Cause = calls.Cause,
			Outcome = calls.Outcome,
			FeedId = FeedOf(calls.Feed),
		};
		rows.Add(ctx.RemoteUsage.Add(row).Entity);
		return row;
	}

	private static RemoteAttemptRow Added(SammlarrDbContext ctx, List<RemoteAttemptRow> rows, UsageAttempts attempts)
	{
		var row = new RemoteAttemptRow
		{
			Service = attempts.Service,
			Hour = attempts.Hour,
			Cause = attempts.Cause,
			FeedId = FeedOf(attempts.Feed),
		};
		rows.Add(ctx.RemoteAttempts.Add(row).Entity);
		return row;
	}

	private static RemoteAttemptBucketRow Added(List<RemoteAttemptBucketRow> buckets, int upToMs)
	{
		var bucket = new RemoteAttemptBucketRow { UpToMs = upToMs };
		buckets.Add(bucket);
		return bucket;
	}

	private static UsageCalls Calls(RemoteUsageRow row)
	{
		return new(row.Service, row.Hour, row.Cause, row.Outcome, Optional.Of(row.FeedId).Map(id => FeedId.From(id)), row.Calls);
	}

	private static UsageAttempts Attempts(RemoteAttemptRow row)
	{
		return new UsageAttempts(
			row.Service,
			row.Hour,
			row.Cause,
			Optional.Of(row.FeedId).Map(id => FeedId.From(id)),
			row.Attempts,
			row.Buckets.ToDictionary(bucket => bucket.UpToMs, bucket => bucket.Count)
		);
	}

	private static Guid? FeedOf(Optional<FeedId> feed) => feed.Map(found => found.Value).ToNullable();
}
