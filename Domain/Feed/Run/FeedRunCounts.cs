using Domain.Feed.Planning;
using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Run;

public readonly record struct FeedRunCounts
{
	public required int Total { get; init; }
	public required int Added { get; init; }
	public required int Kept { get; init; }
	public required int Bypassed { get; init; }
	public required int Filtered { get; init; }
	public required int Skipped { get; init; }
	public required int Unresolved { get; init; }
	public required int Removed { get; init; }

	public static FeedRunCounts None { get; } =
		new()
		{
			Total = 0,
			Added = 0,
			Kept = 0,
			Bypassed = 0,
			Filtered = 0,
			Skipped = 0,
			Unresolved = 0,
			Removed = 0,
		};

	public static FeedRunCounts From(IFeedPlan plan) => From(plan, plan.Delta);

	public static FeedRunCounts From(IFeedPlan plan, FeedDelta delta)
	{
		var buckets = plan.Verdicts.CountBy(ChangeBuckets.Of).ToDictionary();

		return new FeedRunCounts
		{
			Total = plan.Verdicts.Count + plan.Unresolved.Count,
			Added = delta.Adds.Count,
			Kept = buckets.GetValueOrDefault(ChangeBucket.Kept),
			Bypassed = buckets.GetValueOrDefault(ChangeBucket.Bypassed),
			Filtered = buckets.GetValueOrDefault(ChangeBucket.Filtered),
			Skipped = buckets.GetValueOrDefault(ChangeBucket.Skipped),
			Unresolved = plan.Unresolved.Count,
			Removed = delta.Drops.Count,
		};
	}
}
