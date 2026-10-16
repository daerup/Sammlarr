using System.ComponentModel;

namespace Domain.Jobs;

public enum JobKind
{
	FeedRun,
	WantlistSync,

	[Description("Filling details")]
	DetailsFill,
}
