using System.ComponentModel;
using API.Http.Lanes;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Processing;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Harness;
using Microsoft.Extensions.DependencyInjection;
using Shared.Concurrency;
using Shared.Options;
using TestInfrastructure;
using static Harness.FeedItemSpec;
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
		var feedId = await host.SeedFeedAsync("stalled-list");
		host.MdblistFeedContains("stalled-list", Item("Heat", MediaType.Movie, Tmdb("949")) with { Year = 1995 });
		await host.WithPlexAccount("plex-token");
		await host.PlexMatchesNothing(new MediaSpec("Heat", MediaType.Movie, [Tmdb("949")]));
		host.Resolve<PlexOptions>().Http.MaxWaitSeconds = (int)TimeSpan.FromHours(2).TotalSeconds;
		await host.RateLimitedAsync(RemoteService.Plex, minutes: 60);

		var running = host.Resolve<IFeedRunEngine>().RunAsync(feedId, new FeedRunTrigger(new Manual()));
		await SammlarrTestHost.EventuallyAsync(() =>
			host.Resolve<Lanes>().Gate(new LaneName(RemoteService.Plex, Lanes.Rest), new None()).Load(LaneClass.Title).Waiting == 1
		);
		time.Advance(new FeedLoadOptions().StallAfter);

		var completed = await Assert.That(await running.WaitAsync(Patience)).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(completed!.Conclusiveness).IsEqualTo(Conclusiveness.Unresolved);
	}

	[Test]
	public async Task NeedsAsync_ForASortOnGenres_RequiresGenresAndAsksForWhatACardShows()
	{
		var feed = TestFeeds.Feed(Guid.NewGuid()) with
		{
			Sort = Some.Of(new QuerySort { Property = nameof(MediaQueryRow.Genres), Direction = ListSortDirection.Ascending }),
		};

		var needs = await host.Resolve<IFeedRunPlanner>().NeedsAsync(feed);

		await Assert.That(needs.Required).IsEqualTo(MediaField.Genres);
		await Assert.That(needs.Shown.Overlaps(MediaField.Poster)).IsTrue();
	}

	[Test]
	public async Task PlanAsync_TwiceInOneScope_StartsEachLoadWithEmptyMemos()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await host.SeedFeedAsync("memo-list");
		host.MdblistFeedContains("memo-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();
		var planner = host.Resolve<IFeedRunPlanner>();

		await planner.PlanAsync(new SavedFeed(feedId));
		await planner.PlanAsync(new SavedFeed(feedId));

		await SammlarrTestHost.EventuallyAsync(() => host.HttpStub.Requests($"{TestUrls.MdblistApi}/tmdb/movie") == 2);
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
		var feedId = await host.SeedFeedAsync("owners-list");
		host.MdblistFeedEmpty("owners-list");
		await host.SignInAs("Stranger");
		await using var scope = host.Resolve<IServiceScopeFactory>().CreateAsyncScope();
		using var acting = scope.ServiceProvider.GetRequiredService<IUserScope>().As(owner);

		var result = await scope.ServiceProvider.GetRequiredService<IFeedRunPlanner>().PlanAsync(new SavedFeed(feedId));

		var plan = await Assert.That(result).IsOfUnionType<FeedPlanResult, AppendPlan>();
		await Assert.That(plan!.SourceFailure).IsNone();
	}
}
