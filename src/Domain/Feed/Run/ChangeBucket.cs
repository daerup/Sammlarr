using Domain.Processing;

namespace Domain.Feed.Run;

public enum ChangeBucket
{
	Added,
	Kept,
	Bypassed,
	Filtered,
	Skipped,
}

public static class ChangeBuckets
{
	public static ChangeBucket Of<T>(QueryVerdict<T> verdict) =>
		verdict.Outcome switch
		{
			Culled => ChangeBucket.Filtered,
			Truncated => ChangeBucket.Skipped,
			Qualified => OfQualified(verdict),
		};

	private static ChangeBucket OfQualified<T>(QueryVerdict<T> verdict) =>
		verdict.Kept ? ChangeBucket.Kept
		: verdict.WillImport ? ChangeBucket.Added
		: ChangeBucket.Bypassed;
}
