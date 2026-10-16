using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Observability.Diagnostics;
using Observability.Feed;
using Serilog.Core;

namespace Shared.Tests.Observability;

public class InMemoryActivityProcessorTests
{
	[Test]
	public async Task OnEnd_ForAnErroredFeedRunSpan_CapturesIt()
	{
		var capture = Capturing();
		using var listener = Listen();
		using var source = new ActivitySource(FeedRunSpans.SourceName);
		using var span = source.StartActivity(FeedRunNames.Hop);
		span!.SetStatus(ActivityStatusCode.Error, "TMDB is down");
		span.Stop();

		new InMemoryActivityProcessor(capture).OnEnd(span);

		await Assert.That(capture.SnapshotSpans().Select(entry => entry.StatusDescription)).IsEquivalentTo(new string?[] { "TMDB is down" });
	}

	[Test]
	public async Task OnEnd_ForAnAnsweredFeedRunSpan_LeavesItOut()
	{
		var capture = Capturing();
		using var listener = Listen();
		using var source = new ActivitySource(FeedRunSpans.SourceName);
		using var span = source.StartActivity(FeedRunNames.Hop);
		span!.Stop();

		new InMemoryActivityProcessor(capture).OnEnd(span);

		await Assert.That(capture.SnapshotSpans()).IsEmpty();
	}

	private static LogCaptureService Capturing()
	{
		var capture = new LogCaptureService(new LoggingLevelSwitch());
		capture.Start(LogLevel.Information);
		return capture;
	}

	private static ActivityListener Listen()
	{
		var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == FeedRunSpans.SourceName,
			Sample = (ref _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		return listener;
	}
}
