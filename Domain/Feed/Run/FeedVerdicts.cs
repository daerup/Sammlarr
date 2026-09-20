using Domain.Processing;

namespace Domain.Feed.Run;

public static class FeedVerdicts
{
	extension<T>(QueryVerdict<T> verdict)
	{
		public bool WillImport => verdict.Outcome is QueryOutcome.Qualified && !verdict.Enrollment.InWatchlist && !verdict.Enrollment.InBlacklist;

		public bool Kept => verdict.Outcome is QueryOutcome.Qualified && verdict.Enrollment.Owned;

		public bool Bypassed =>
			verdict.Outcome is QueryOutcome.Qualified && !verdict.Enrollment.Owned && (verdict.Enrollment.InWatchlist || verdict.Enrollment.InBlacklist);

		public bool AtRisk => verdict.Enrollment.Owned && verdict.Outcome is QueryOutcome.Culled or QueryOutcome.Truncated;
	}
}
