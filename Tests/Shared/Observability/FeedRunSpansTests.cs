using System.Collections.Concurrent;
using System.Diagnostics;
using Domain.Feed;
using Observability.Feed;
using TestInfrastructure;

namespace Shared.Tests.Observability;

public class FeedRunSpansTests
{
	[Test]
	public async Task Title_InsideARun_LeavesTheCurrentSpanAlone()
	{
		using var listener = Listen();
		_ = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());

		var title = FeedRunSpans.Title("Heat", Some.Of(1995));

		await Assert.That(title).IsSome();
		await Assert.That(Activity.Current?.OperationName).IsEqualTo(FeedRunNames.Run);
	}

	[Test]
	public async Task Enter_RestoresTheOuterSpanOnDispose()
	{
		using var listener = Listen();
		_ = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());
		var title = FeedRunSpans.Title("Heat", new None());

		using (FeedRunSpans.Enter(title))
		{
			await Assert.That(Activity.Current?.OperationName).IsEqualTo(FeedRunNames.Title);
		}

		await Assert.That(Activity.Current?.OperationName).IsEqualTo(FeedRunNames.Run);
	}

	[Test]
	public async Task SourcePage_InsideARun_RecordsThePageAndItsTotal()
	{
		var stopped = new ConcurrentQueue<Activity>();
		using var listener = Listen(stopped.Enqueue);
		var run = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());

		FeedRunSpans.SourcePage(3, Some.Of(12));

		var traceId = run.Match(span => span.TraceId, () => default);
		var page = stopped.Single(span => span.OperationName == FeedRunNames.Page && span.TraceId == traceId);
		await Assert.That(page.GetTagItem(FeedRunTags.Page)).IsEqualTo(3);
		await Assert.That(page.GetTagItem(FeedRunTags.Pages)).IsEqualTo(12);
	}

	[Test]
	public async Task SourcePage_OutsideARun_RecordsNothing()
	{
		var stopped = new ConcurrentQueue<Activity>();
		using var listener = Listen(stopped.Enqueue);

		FeedRunSpans.SourcePage(1, new None());

		await Assert.That(stopped.Where(span => span.OperationName == FeedRunNames.Page && span.Parent is null)).IsEmpty();
	}

	private static ActivityListener Listen(Action<Activity>? stopped = null)
	{
		var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == FeedRunSpans.SourceName,
			Sample = (ref _) => ActivitySamplingResult.AllData,
			ActivityStopped = stopped,
		};
		ActivitySource.AddActivityListener(listener);
		return listener;
	}
}
