using System.Collections.Concurrent;
using System.Diagnostics;
using Domain.Feed;
using Domain.Feed.Trace;
using Observability.Feed;

namespace API.Tests;

internal sealed class PageSpans : IDisposable
{
	private readonly ConcurrentQueue<SourcePage> _pages = new();

	private readonly ActivityListener _listener;

	private readonly Optional<Activity> _run;

	internal PageSpans()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == FeedRunSpans.SourceName,
			Sample = (ref _) => ActivitySamplingResult.AllData,
			ActivityStopped = Stopped,
		};
		ActivitySource.AddActivityListener(_listener);
		_run = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());
	}

	internal IReadOnlyList<SourcePage> Pages => [.. _pages];

	public void Dispose()
	{
		_run.Apply(span => span.Stop());
		_listener.Dispose();
	}

	private void Stopped(Activity span)
	{
		var mine = _run.Match(run => run.TraceId == span.TraceId, () => false);
		if (mine && span.OperationName == FeedRunNames.Page && span.GetTagItem(FeedRunTags.Page) is int page)
		{
			_pages.Enqueue(new SourcePage(page, Optional.Of(span.GetTagItem(FeedRunTags.Pages) as int?)));
		}
	}
}
