using Contracts.Housekeeping;
using DB.Data.Feed;
using DB.Data.Feed.Run;
using DB.Data.Jobs;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Jobs;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Harness;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;

namespace Integration.Tests.Housekeeping;

internal static class HousekeepingSeeds
{
	extension(SammlarrTestHost host)
	{
		public IHousekeepingStep Step(HousekeepingStep step) => host.Resolve<IEnumerable<IHousekeepingStep>>().Single(registered => registered.Step == step);

		public async Task<Guid> SeedFeedAsync()
		{
			await using var ctx = await host.ContextFactory.CreateDbContextAsync();
			var feed = new FeedRow
			{
				Id = Guid.NewGuid(),
				IsEnabled = true,
				Name = "Trending",
				Provider = FeedProvider.Tmdb,
			};
			ctx.Feeds.Add(feed);
			await ctx.SaveChangesAsync();
			return feed.Id;
		}

		public async Task<Guid> SeedRunAsync(Guid feed, DateTimeOffset startedAt, Guid title)
		{
			await using var ctx = await host.ContextFactory.CreateDbContextAsync();
			var run = new FeedRunRow
			{
				Id = Guid.NewGuid(),
				FeedId = feed,
				StartedAt = startedAt,
				Added = 1,
				Items =
				[
					new FeedRunItemRow
					{
						Id = Guid.NewGuid(),
						MediaId = title,
						State = FeedRunItemState.Added,
					},
				],
				Calls =
				[
					new FeedRunCallsRow
					{
						Id = Guid.NewGuid(),
						Service = RemoteService.Tmdb,
						Outcome = UsageOutcome.Answered,
						Calls = 1,
					},
				],
				Times =
				[
					new FeedRunTimesRow
					{
						Id = Guid.NewGuid(),
						Service = RemoteService.Tmdb,
						UpToMs = 100,
						Attempts = 1,
					},
				],
			};
			ctx.FeedRuns.Add(run);
			await ctx.SaveChangesAsync();
			return run.Id;
		}

		public async Task<Guid> SeedJobAsync(JobState state, DateTimeOffset settledAt)
		{
			await using var ctx = await host.ContextFactory.CreateDbContextAsync();
			var job = new JobRow
			{
				Id = Guid.NewGuid(),
				OwnerId = SammlarrTestHost.SeededAdmin.Id,
				Kind = JobKind.FeedRun,
				Key = Guid.NewGuid().ToString(),
				Request = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()),
				State = state,
				RunAfter = DateTimeOffset.UnixEpoch,
				Deadline = DateTimeOffset.UnixEpoch,
			};
			ctx.Jobs.Add(job);
			await ctx.SaveChangesAsync();
			await ctx.Jobs.Where(row => row.Id == job.Id).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.UpdatedAt, settledAt));
			return job.Id;
		}

		public async Task<Guid> SeedTickerAsync()
		{
			await using var ctx = await host.ContextFactory.CreateDbContextAsync();
			var ticker = new CronTickerEntity
			{
				Id = Guid.NewGuid(),
				Function = "RunFeed",
				Expression = "0 0 * * * *",
				IsEnabled = true,
			};
			ctx.Set<CronTickerEntity>().Add(ticker);
			await ctx.SaveChangesAsync();
			return ticker.Id;
		}

		public async Task<Guid> SeedOccurrenceAsync(Guid ticker, TickerStatus status, DateTimeOffset executionTime)
		{
			await using var ctx = await host.ContextFactory.CreateDbContextAsync();
			var occurrence = new CronTickerOccurrenceEntity<CronTickerEntity>
			{
				Id = Guid.NewGuid(),
				CronTickerId = ticker,
				Status = status,
				ExecutionTime = executionTime.UtcDateTime,
			};
			ctx.Set<CronTickerOccurrenceEntity<CronTickerEntity>>().Add(occurrence);
			await ctx.SaveChangesAsync();
			return occurrence.Id;
		}

		public async Task AddedAsync(MediaId id, DateTimeOffset at)
		{
			await using var ctx = await host.ContextFactory.CreateDbContextAsync();
			await ctx.Media.Where(media => media.Id == id.Value).ExecuteUpdateAsync(setters => setters.SetProperty(media => media.CreatedAt, at));
		}
	}
}
