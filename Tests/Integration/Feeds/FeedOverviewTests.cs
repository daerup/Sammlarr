using Contracts.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedOverviewTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IFeedService Testee => host.Resolve<IFeedService>();

	[Test]
	public async Task GetAsync_ForAFeedThatAddedTitles_CountsOnlyWhatItPutOnTheWantlist()
	{
		var comedy = await Testee.CreateAsync(Feed("Comedy"));
		var origin = await host.SeedFeedOriginAsync(comedy);
		await host.Movie(TestMedia.Inception).OnWantlist(origin);
		await host.Movie(TestMedia.FightClub).OnWantlist(origin);
		await host.Movie(TestMedia.Interstellar).OnWantlist(new ManualOrigin(ManualSource.Search));

		var overview = await Assert.That((await Testee.GetAsync()).Find(comedy)).IsSome();

		await Assert.That(overview!.OnWantlist).IsEqualTo(2);
		await Assert.That(overview.LastRun).IsSome();
		await Assert.That(overview.History.TotalRuns).IsEqualTo(1);
	}

	[Test]
	public async Task GetAsync_ForAFeedThatNeverRan_HasNoLastRunAndNothingOnTheWantlist()
	{
		var fresh = await Testee.CreateAsync(Feed("Fresh"));

		var overview = await Assert.That((await Testee.GetAsync()).Find(fresh)).IsSome();

		await Assert.That(overview!.LastRun).IsNone();
		await Assert.That(overview.History).IsEqualTo(FeedRunHistory.None);
		await Assert.That(overview.OnWantlist).IsEqualTo(0);
	}

	private static FeedCreateData Feed(string name) =>
		new()
		{
			Name = name,
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = name.ToLowerInvariant(),
			Schedule = new FeedRunTrigger(new Manual()),
		};
}
