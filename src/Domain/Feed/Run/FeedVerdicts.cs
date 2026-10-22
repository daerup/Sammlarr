using Domain.Processing;

namespace Domain.Feed.Run;

public static class FeedVerdicts
{
	extension<T>(QueryVerdict<T> verdict)
	{
		public bool WillImport => verdict.Outcome is Qualified && !verdict.Enrollment.InWantlist && !verdict.Enrollment.InBlocklist;

		public bool Kept => verdict.Outcome is Qualified && verdict.Enrollment.Owned;

		public bool Bypassed => verdict.Outcome is Qualified && !verdict.Enrollment.Owned && (verdict.Enrollment.InWantlist || verdict.Enrollment.InBlocklist);

		public bool AtRisk => verdict.Enrollment.Owned && verdict.Outcome is Culled or Truncated;
	}
}
