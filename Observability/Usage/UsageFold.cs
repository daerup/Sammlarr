using Contracts.Usage;

namespace Observability.Usage;

internal static class UsageFold
{
	internal static UsageRows Of(IEnumerable<UsageCalls> calls, IEnumerable<UsageAttempts> attempts)
	{
		IReadOnlyList<UsageCalls> foldedCalls =
		[
			.. calls
				.GroupBy(row => (row.Service, row.Hour, row.Cause, row.Outcome, Feed: FeedKey(row.Feed)))
				.Select(group => group.First() with { Calls = group.Sum(row => row.Calls) })
				.Where(row => row.Calls > 0),
		];
		IReadOnlyList<UsageAttempts> foldedAttempts =
		[
			.. attempts
				.GroupBy(row => (row.Service, row.Hour, row.Cause, Feed: FeedKey(row.Feed)))
				.Select(group => group.First() with { Attempts = group.Sum(row => row.Attempts), Buckets = Merged(group.Select(row => row.Buckets)) })
				.Where(row => row.Attempts > 0),
		];
		return new UsageRows(foldedCalls, foldedAttempts);
	}

	private static IReadOnlyDictionary<int, long> Merged(IEnumerable<IReadOnlyDictionary<int, long>> buckets)
	{
		return buckets.SelectMany(each => each).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));
	}

	private static string FeedKey(Optional<Domain.Feed.FeedId> feed) => feed.Match(found => found.ToString(), () => string.Empty);
}
