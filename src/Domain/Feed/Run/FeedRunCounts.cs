using Domain.Feed.Planning;
using Domain.Media;

namespace Domain.Feed.Run;

public readonly record struct FeedRunCounts
{
	public required int Total { get; init; }
	public required int Added { get; init; }
	public required int Kept { get; init; }
	public required int Bypassed { get; init; }
	public required int Filtered { get; init; }
	public required int Skipped { get; init; }
	public required int UnmatchedNotFound { get; init; }
	public required int UnmatchedCouldNotLookUp { get; init; }
	public required int UnmatchedConflict { get; init; }
	public required int Removed { get; init; }
	public required int Known { get; init; }
	public required int NotFoundEarlier { get; init; }

	public int Unmatched => UnmatchedNotFound + UnmatchedCouldNotLookUp + UnmatchedConflict;

	public static FeedRunCounts None { get; } =
		new()
		{
			Total = 0,
			Added = 0,
			Kept = 0,
			Bypassed = 0,
			Filtered = 0,
			Skipped = 0,
			UnmatchedNotFound = 0,
			UnmatchedCouldNotLookUp = 0,
			UnmatchedConflict = 0,
			Removed = 0,
			Known = 0,
			NotFoundEarlier = 0,
		};

	public static FeedRunCounts From(IFeedPlan plan) => From(plan, plan.Delta);

	public static FeedRunCounts From(IFeedPlan plan, FeedDelta delta)
	{
		var buckets = plan.Verdicts.CountBy(ChangeBuckets.Of).ToDictionary();

		return new FeedRunCounts
		{
			Total = plan.Verdicts.Count + plan.Unmatched.Count,
			Added = delta.Adds.Count,
			Kept = buckets.GetValueOrDefault(ChangeBucket.Kept),
			Bypassed = buckets.GetValueOrDefault(ChangeBucket.Bypassed),
			Filtered = buckets.GetValueOrDefault(ChangeBucket.Filtered),
			Skipped = buckets.GetValueOrDefault(ChangeBucket.Skipped),
			UnmatchedNotFound = plan.Unmatched.Count(unmatched => unmatched.Reason is UnmatchedReason.NotFound or UnmatchedReason.NotFoundEarlier),
			UnmatchedCouldNotLookUp = plan.Unmatched.Count(unmatched => unmatched.Reason is UnmatchedReason.CouldNotLookUp),
			UnmatchedConflict = plan.Unmatched.Count(unmatched => unmatched.Reason is UnmatchedReason.Conflict),
			Removed = delta.Drops.Count,
			Known = plan.Verdicts.Count(verdict => verdict.Item.Known),
			NotFoundEarlier = plan.Unmatched.Count(unmatched => unmatched.Reason is UnmatchedReason.NotFoundEarlier),
		};
	}
}
