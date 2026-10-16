using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class RunCallsViewTests
{
	private static readonly FeedRun Run = TestFeeds.Run(TestFeeds.Feed(Guid.NewGuid()), Guid.NewGuid());

	[Test]
	public async Task Services_OfARun_JoinEachServicesCallsWithItsAnswerTimesBusiestFirst()
	{
		var run = Run with
		{
			Calls =
			[
				new FeedRunCalls(RemoteService.Tmdb, UsageOutcome.Answered, 290),
				new FeedRunCalls(RemoteService.Plex, UsageOutcome.Answered, 520),
				new FeedRunCalls(RemoteService.Plex, UsageOutcome.AnsweredAfter429, 37),
			],
			Times = [new FeedRunTimes(RemoteService.Plex, 250, 500), new FeedRunTimes(RemoteService.Plex, 500, 94), new FeedRunTimes(RemoteService.Tmdb, 100, 290)],
		};

		var services = RunCallsView.Services(run);

		await Assert
			.That(services.Select(line => (line.Service, line.Tally.Total, line.Tally.Durations.Values.Sum())))
			.IsEquivalentTo([(RemoteService.Plex, 557L, 594L), (RemoteService.Tmdb, 290L, 290L)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Typical_OfTheFeedsRecentRuns_IsTheMedianOfThoseThatMadeCalls()
	{
		FeedRun[] runs = [Calling(400), Calling(0), Calling(100), Calling(300), Calling(200)];

		var typical = RunCallsView.Typical(runs);

		await Assert.That(typical).IsSome().And.IsEqualTo(new RunTypical(250, 4));
	}

	[Test]
	[Arguments(8287L, 4664d, "1.8× this feed's typical run", "trending-up")]
	[Arguments(105L, 100d, "About the same as this feed's typical run", "minus")]
	[Arguments(50L, 100d, "50% under this feed's typical run", "trending-down")]
	public async Task Versus_TheTypicalRun_SaysHowFarTheRunIsFromIt(long calls, double typical, string text, string icon)
	{
		await Assert.That(RunCallsView.Versus(calls, typical)).IsEqualTo((text, icon));
	}

	[Test]
	public async Task Basis_OfTheTypicalRun_NamesItsCallsAndHowManyRunsItCameFrom()
	{
		await Assert.That(RunCallsView.Basis(new RunTypical(4664, 12))).Contains("4,664").And.Contains("12 runs");
	}

	private static FeedRun Calling(long calls)
	{
		return Run with { Calls = calls == 0 ? [] : [new FeedRunCalls(RemoteService.Tmdb, UsageOutcome.Answered, calls)] };
	}
}
