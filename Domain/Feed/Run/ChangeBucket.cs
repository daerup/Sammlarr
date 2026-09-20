using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Run;

public enum ChangeBucket
{
	Added,
	Kept,
	Bypassed,
	Filtered,
	Skipped,
	Unresolved,
}

public static class ChangeBuckets
{
	public static ChangeBucket Of<T>(QueryVerdict<T> verdict) =>
		verdict.Outcome switch
		{
			QueryOutcome.Culled => ChangeBucket.Filtered,
			QueryOutcome.Truncated => ChangeBucket.Skipped,
			QueryOutcome.Qualified => Qualified(verdict),
		};

	private static ChangeBucket Qualified<T>(QueryVerdict<T> verdict) =>
		verdict.Kept ? ChangeBucket.Kept
		: verdict.WillImport ? ChangeBucket.Added
		: ChangeBucket.Bypassed;
}
