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

	[Test]
	public async Task ReadingAsync_InsideARun_SpansTheFetchAndReturnsItsAnswer()
	{
		var stopped = new ConcurrentQueue<Activity>();
		using var listener = Listen(stopped.Enqueue);
		var run = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());

		var answer = await FeedRunSpans.ReadingAsync(4, () => Task.FromResult("page four"));

		var traceId = run.Match(span => span.TraceId, () => default);
		var reading = stopped.Single(span => span.OperationName == FeedRunNames.Reading && span.TraceId == traceId);
		await Assert.That(answer).IsEqualTo("page four");
		await Assert.That(reading.GetTagItem(FeedRunTags.Page)).IsEqualTo(4);
	}

	[Test]
	public async Task ReadingAsync_OutsideARun_RecordsNothingAndStillReads()
	{
		var stopped = new ConcurrentQueue<Activity>();
		using var listener = Listen(stopped.Enqueue);

		var answer = await FeedRunSpans.ReadingAsync(1, () => Task.FromResult(7));

		await Assert.That(answer).IsEqualTo(7);
		await Assert.That(stopped.Where(span => span.OperationName == FeedRunNames.Reading && span.Parent is null)).IsEmpty();
	}

	[Test]
	public async Task SourceAsync_WhenTheConsumerStopsEarly_EndsTheSourceSpanWithTheCountTaken()
	{
		var stopped = new ConcurrentQueue<Activity>();
		using var listener = Listen(stopped.Enqueue);
		var run = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());

		await foreach (var item in FeedRunSpans.SourceAsync(Enumerable.Range(0, 100).ToAsyncEnumerable(), _ => true))
		{
			if (item == 2)
			{
				break;
			}
		}

		var traceId = run.Match(span => span.TraceId, () => default);
		var source = stopped.Single(span => span.OperationName == FeedRunNames.Source && span.TraceId == traceId);
		await Assert.That(source.GetTagItem(FeedRunTags.Requests)).IsEqualTo(3);
	}

	[Test]
	public async Task SourceAsync_ForItemsThatAreNoRequests_CountsOnlyTheRequests()
	{
		var stopped = new ConcurrentQueue<Activity>();
		using var listener = Listen(stopped.Enqueue);
		var run = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());

		await FeedRunSpans.SourceAsync(Enumerable.Range(0, 10).ToAsyncEnumerable(), item => item % 2 == 0).ToListAsync();

		var traceId = run.Match(span => span.TraceId, () => default);
		var source = stopped.Single(span => span.OperationName == FeedRunNames.Source && span.TraceId == traceId);
		await Assert.That(source.GetTagItem(FeedRunTags.Requests)).IsEqualTo(5);
	}

	[Test]
	public async Task SourceAsync_WhenTheSourceThrows_EndsTheSourceSpan()
	{
		var stopped = new ConcurrentQueue<Activity>();
		using var listener = Listen(stopped.Enqueue);
		var run = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());

		var act = async () => await FeedRunSpans.SourceAsync(Failing(), _ => true).ToListAsync();

		await Assert.That(act).Throws<InvalidOperationException>();
		var traceId = run.Match(span => span.TraceId, () => default);
		var source = stopped.Single(span => span.OperationName == FeedRunNames.Source && span.TraceId == traceId);
		await Assert.That(source.GetTagItem(FeedRunTags.Requests)).IsEqualTo(1);
	}

	private static async IAsyncEnumerable<int> Failing()
	{
		yield return 1;
		await Task.Yield();
		throw new InvalidOperationException("the list failed");
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
