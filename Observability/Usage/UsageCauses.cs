using System.Diagnostics;
using Domain.Feed;
using Domain.Usage;

namespace Observability.Usage;

public readonly record struct UsageMark(UsageCause Cause, Optional<FeedId> Feed)
{
	public Optional<FeedId> Run => Feed.When(Cause == UsageCause.FeedRun);
}

public static class UsageCauses
{
	private static readonly AsyncLocal<UsageMark?> Marked = new();

	public static UsageMark Current => Optional.Of(Marked.Value).GetValueOr(new UsageMark(UsageCause.Other, new None()));

	public static IDisposable Mark(UsageCause cause) => Mark(new UsageMark(cause, new None()));

	public static IDisposable Mark(FeedId feed) => Mark(new UsageMark(UsageCause.FeedRun, Some.Of(feed)));

	public static IDisposable Mark(UsageCause cause, Optional<FeedId> feed) => Mark(new UsageMark(cause, feed));

	private static IDisposable Mark(UsageMark mark)
	{
		var outer = Marked.Value;
		if (outer is not null)
		{
			return new Unmark(outer);
		}

		Marked.Value = mark;
		Activity.Current?.SetTag(UsageTags.Cause, mark.Cause.ToString());
		mark.Feed.Apply(feed => Activity.Current?.SetTag(UsageTags.Feed, feed.ToString()));
		return new Unmark(null);
	}

	private sealed class Unmark(UsageMark? restore) : IDisposable
	{
		public void Dispose() => Marked.Value = restore;
	}
}
