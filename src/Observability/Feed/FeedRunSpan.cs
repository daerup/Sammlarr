using System.Diagnostics;
using Domain.Feed.Trace;

namespace Observability.Feed;

public sealed class FeedRunSpan : IDisposable
{
	private readonly Optional<Activity> _span;

	internal FeedRunSpan(Optional<Activity> span) => _span = span;

	public void Answered(HopOutcome outcome) => _span.Apply(found => found.SetTag(FeedRunTags.Answer, outcome));

	public void Dispose() => _span.Apply(found => found.Stop());
}
