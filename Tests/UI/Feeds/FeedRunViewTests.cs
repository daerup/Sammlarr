using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using TestInfrastructure;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class FeedRunViewTests
{
	private static readonly Feed Comedy = TestFeeds.Feed(new Guid(1, 0, 0, new byte[8]), "Comedy");

	[Test]
	public async Task Problem_WhenARunFailedReadingTheSource_SaysTheSourceCouldNotBeRead()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.NoItems) with { SourceFailure = Some.Of("HTTP 404") };

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The source could not be read, so nothing was applied.");
	}

	[Test]
	public async Task Problem_WhenARunFailedWithoutASourceFailure_SaysTheSourceReturnedNothing()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.NoItems);

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The source returned nothing, so nothing was applied.");
	}

	[Test]
	public async Task Problem_WhenARunStillLoadingFailedPartway_SaysTheSourceFailedPartway()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.StillLoading) with
		{
			Counts = FeedRunCounts.None with { Total = 3 },
			SourceFailure = Some.Of("HTTP 500"),
		};

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The source failed partway through, so no removals were applied.");
	}

	[Test]
	public async Task Problem_WhenARunWasStillLoadingWithoutAFailure_SaysTheSourceWasStillLoading()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.StillLoading) with { Counts = FeedRunCounts.None with { Total = 3 } };

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The source was still loading, so no removals were applied.");
	}

	[Test]
	public async Task Problem_WhenAPlanSourceFailedAfterLoadingSome_SaysTheRunWouldHoldBackRemovals()
	{
		var plan = new AppendPlan([], [new UnmatchedMedia([], new MovieDetails())], FeedDelta.None, Conclusiveness.StillLoading)
		{
			SourceFailure = Some.Of("HTTP 500"),
		};

		await Assert.That(FeedRunView.Problem(plan)).IsEqualTo("The source failed partway through, so this run would hold back removals.");
	}

	[Test]
	public async Task Problem_WhenAPlanHasNoSourceFailure_IsNull()
	{
		var plan = new AppendPlan([], [], FeedDelta.None, Conclusiveness.Conclusive);

		await Assert.That(FeedRunView.Problem(plan)).IsNull();
	}

	[Test]
	public async Task Problem_WhenAPlanSourceFailedBeforeLoadingAnything_SaysTheRunWouldChangeNothing()
	{
		var plan = new AppendPlan([], [], FeedDelta.None, Conclusiveness.NoItems) { SourceFailure = Some.Of("HTTP 404") };

		await Assert.That(FeedRunView.Problem(plan)).IsEqualTo("The source could not be read, so this run would change nothing.");
	}
}
