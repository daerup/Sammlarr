using Contracts.Usage;
using CsCheck;
using Domain.Feed;
using Domain.Providers;
using Domain.Usage;
using Observability.Usage;

namespace Shared.Tests.Usage;

public class UsageFoldTests
{
	private static readonly DateTimeOffset Hour = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

	private static readonly FeedId Feed = FeedId.From(Guid.NewGuid());

	[Test]
	public async Task Of_TwoDeltasOfOneHour_FoldIntoOneRow()
	{
		var rows = UsageFold.Of(
			[Calls(UsageOutcome.Answered, 2), Calls(UsageOutcome.Answered, 3), Calls(UsageOutcome.Failed, 1)],
			[Attempts(2, new() { [100] = 2 }), Attempts(4, new() { [100] = 1, [250] = 3 })]
		);

		await Assert.That(rows.Calls.Select(row => (row.Outcome, row.Calls))).IsEquivalentTo([(UsageOutcome.Answered, 5L), (UsageOutcome.Failed, 1L)]);
		await Assert.That(rows.Attempts.Select(row => row.Attempts)).IsEquivalentTo([6L]);
		await Assert.That(rows.Attempts.Single().Buckets).IsEquivalentTo(new Dictionary<int, long> { [100] = 3, [250] = 3 });
	}

	[Test]
	public async Task Of_EmptyDeltas_AreDropped()
	{
		var rows = UsageFold.Of([Calls(UsageOutcome.Answered, 0)], [Attempts(0, [])]);

		await Assert.That(rows.Calls).IsEmpty();
		await Assert.That(rows.Attempts).IsEmpty();
	}

	[Test]
	public void Of_ForAnyDeltas_KeepsEveryCallAndOneRowPerKey()
	{
		Gen.Select(
				Gen.OneOf(RemoteService.Tmdb, RemoteService.Plex),
				Gen.Int[0, 2],
				Gen.Enum<UsageOutcome>(),
				Gen.OneOf<Optional<FeedId>>(Some.Of(Feed), new None()),
				Gen.Long[0, 20],
				(service, hour, outcome, feed, calls) => new UsageCalls(service, Hour.AddHours(hour), UsageCause.FeedRun, outcome, feed, calls)
			)
			.List[0, 30]
			.Sample(
				deltas =>
				{
					var folded = UsageFold.Of(deltas, []).Calls;
					var keys = folded.Select(row => (row.Service, row.Hour, row.Outcome, Feed: row.Feed.Map(found => found.Value).ToNullable())).ToList();
					return folded.Sum(row => row.Calls) == deltas.Sum(row => row.Calls) && keys.Distinct().Count() == keys.Count;
				},
				iter: 5_000
			);
	}

	private static UsageCalls Calls(UsageOutcome outcome, long calls) => new(RemoteService.Tmdb, Hour, UsageCause.FeedRun, outcome, Some.Of(Feed), calls);

	private static UsageAttempts Attempts(long attempts, Dictionary<int, long> buckets)
	{
		return new(RemoteService.Tmdb, Hour, UsageCause.FeedRun, Some.Of(Feed), attempts, buckets);
	}
}
