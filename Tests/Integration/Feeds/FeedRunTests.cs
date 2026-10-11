using API.Http.Lanes;
using Contracts.Feed;
using Contracts.Identity;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.Extensions.DependencyInjection;
using Shared.Concurrency;
using Shared.Options;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Run_WithAServiceBlockedForever_EndsAfterStallAfterWithEveryTitleSettled()
	{
		var time = host.UseFakeTime();
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("stalled-list");
		host.MdblistFeedContains("stalled-list", Item("Heat", MediaType.Movie, Tmdb("949")) with { Year = 1995 });
		await host.WithPlexAccount("plex-token");
		await host.PlexFindsNothing("Heat");
		host.Resolve<PlexOptions>().Http.MaxWaitSeconds = (int)TimeSpan.FromHours(2).TotalSeconds;
		await host.RateLimitedAsync(RemoteService.Plex, minutes: 60);

		var running = host.Resolve<IFeedRunEngine>().RunAsync(feedId, new FeedRunTrigger(new Manual()));
		await EventuallyAsync(() => host.Resolve<Lanes>().Gate(new LaneName(RemoteService.Plex, Lanes.Rest), new None()).Load(LaneClass.Title).Waiting == 1);
		time.Advance(new FeedLoadOptions().StallAfter);

		var completed = await Assert.That(await running.WaitAsync(Patience)).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(completed!.Conclusiveness).IsEqualTo(Conclusiveness.Unresolved);
	}

	[Test]
	public async Task PlanAsync_TwiceInOneScope_StartsEachLoadWithEmptyMemos()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("memo-list");
		host.MdblistFeedContains("memo-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();
		var planner = host.Resolve<IFeedRunPlanner>();

		await planner.PlanAsync(new SavedFeed(feedId));
		await planner.PlanAsync(new SavedFeed(feedId));

		await EventuallyAsync(() => host.HttpStub.Requests($"{TestUrls.MdblistApi}/tmdb/movie") == 2);
	}

	[Test]
	public async Task PlanAsync_InItsLoadScope_UsesTheCallersKeys()
	{
		await host.SignInAs("Owner");
		var owner = new TestCurrentUser
		{
			Id = host.CurrentUser.Id,
			DisplayName = "Owner",
			Role = UserRole.Member,
		};
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("owners-list");
		host.MdblistFeedEmpty("owners-list");
		await host.SignInAs("Stranger");
		await using var scope = host.Resolve<IServiceScopeFactory>().CreateAsyncScope();
		using var acting = scope.ServiceProvider.GetRequiredService<IUserScope>().As(owner);

		var result = await scope.ServiceProvider.GetRequiredService<IFeedRunPlanner>().PlanAsync(new SavedFeed(feedId));

		var plan = await Assert.That(result).IsOfUnionType<FeedPlanResult, AppendPlan>();
		await Assert.That(plan!.SourceFailure).IsNone();
	}

	private static async Task EventuallyAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(Patience);
		while (!condition())
		{
			await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
		}
	}

	private static FeedItemSpec Item(string title, MediaType type, params ProviderId[] ids)
	{
		return new FeedItemSpec(title, type, null, ids.ToDictionary(id => id.Provider, id => id.Value));
	}

	private async Task<FeedId> SeedFeedAsync(string identifier)
	{
		var entity = new FeedRow
		{
			Name = "Comedy",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
