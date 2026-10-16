using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Shared.Tests;

public class AsyncEnumerableExtensionsTests
{
	[Test]
	public async Task BurstsAsync_ForPagesYieldedWithoutWaiting_YieldsOneBurstPerPage()
	{
		var bursts = await Paged([3, 2, 4]).BurstsAsync().ToListAsync();

		await Assert.That(bursts.Select(burst => burst.Count)).IsEquivalentTo([3, 2, 4], CollectionOrdering.Matching);
	}

	[Test]
	public async Task BurstsAsync_WhenTheSourceFailsWithoutWaiting_YieldsThePendingBurstThenThrows()
	{
		var seen = new List<int>();

		var act = async () =>
		{
			await foreach (var burst in FailsAtOnceAfter(3).BurstsAsync())
			{
				seen.AddRange(burst);
			}
		};

		await Assert.That(act).Throws<InvalidOperationException>();
		await Assert.That(seen).IsEquivalentTo([0, 1, 2], CollectionOrdering.Matching);
	}

	[Test]
	public async Task BurstsAsync_WhenCancelled_HandsTheTokenToTheSource()
	{
		using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		var act = async () => await Endless().BurstsAsync(cancellation.Token).ToListAsync();

		await Assert.That(act).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task InListOrderAsync_ForResultsSettledOutOfOrder_YieldsThemByPosition()
	{
		var listed = new[] { new Listed<string>(2, "c"), new Listed<string>(0, "a"), new Listed<string>(1, "b") }.ToAsyncEnumerable();

		var ordered = await listed.InListOrderAsync().ToListAsync();

		await Assert.That(ordered).IsEquivalentTo(["a", "b", "c"], CollectionOrdering.Matching);
	}

	private static async IAsyncEnumerable<int> Paged(int[] pages)
	{
		var next = 0;
		foreach (var size in pages)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(20));
			foreach (var _ in Enumerable.Range(0, size))
			{
				yield return next++;
			}
		}
	}

	private static async IAsyncEnumerable<int> Endless([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		while (true)
		{
			await Task.Delay(1, cancellationToken);
			yield return 0;
		}
	}

	private static async IAsyncEnumerable<int> FailsAtOnceAfter(int count)
	{
		await Task.Yield();
		foreach (var i in Enumerable.Range(0, count))
		{
			yield return i;
		}

		throw new InvalidOperationException("source failed");
	}

	[Test]
	public async Task StreamAsync_WhileTheProducerIsStillRunning_YieldsWhatItHasWritten()
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		var first = await Channel
			.CreateUnbounded<int>()
			.StreamAsync(
				async (writer, token) =>
				{
					await writer.WriteAsync(1, token);
					await Task.Delay(Timeout.Infinite, token);
				},
				timeout.Token
			)
			.FirstAsync(timeout.Token);

		await Assert.That(first).IsEqualTo(1);
	}

	private static async IAsyncEnumerable<int> Counting(int count, Action onProduced)
	{
		foreach (var i in Enumerable.Range(0, count))
		{
			onProduced();
			yield return i;
			await Task.Yield();
		}
	}

	private static async IAsyncEnumerable<int> ThrowsAfter(int count)
	{
		foreach (var i in Enumerable.Range(0, count))
		{
			yield return i;
			await Task.Yield();
		}

		throw new InvalidOperationException("source failed");
	}

	[Test]
	public async Task SelectAwait_WhenSequenceIsNotEnumerated_DoesNotInvokeProjection()
	{
		var source = AsyncSequence.Of(1, 2, 3);
		var wasInvoked = false;
		_ = source.SelectAwait(value =>
		{
			wasInvoked = true;
			return new ValueTask<int>(value * 2);
		});

		await Assert.That(wasInvoked).IsFalse();
	}

	[Test]
	public async Task SelectAwait_WhenSourceIsEnumerated_ProjectsValuesInOrder()
	{
		var source = AsyncSequence.Of(1, 2, 3);
		var testee = source.SelectAwait(async value =>
		{
			await Task.Yield();
			return value * 2;
		});

		var results = await testee.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(3);
		await Assert.That(results[0]).IsEqualTo(2);
		await Assert.That(results[1]).IsEqualTo(4);
		await Assert.That(results[2]).IsEqualTo(6);
	}

	[Test]
	public async Task SelectAwait_Concurrent_RunsItemsUpToConcurrencyLimitInParallel()
	{
		var items = Enumerable.Range(1, 4).Select(v => new GatedItem(v)).ToList();
		var pipeline = AsyncSequence.Of(items).SelectAwait(item => item.InvokeAsync(), maxConcurrency: 4);
		var consume = pipeline.ToListAsync().AsTask();

		await Task.WhenAll(items.Select(i => i.Started)).WaitAsync(TimeSpan.FromSeconds(5));
		items.ForEach(i => i.Complete());

		var results = await consume.WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(results).IsEquivalentTo(items.Select(i => i.Value), CollectionOrdering.Matching);
	}

	[Test]
	public async Task SelectAwait_Concurrent_PreservesSourceOrderEvenWhenLaterItemsCompleteFirst()
	{
		var items = Enumerable.Range(1, 5).Select(v => new GatedItem(v)).ToArray();
		var pipeline = AsyncSequence.Of(items).SelectAwait(item => item.InvokeAsync(), maxConcurrency: 5);
		var consume = pipeline.ToListAsync().AsTask();

		await Task.WhenAll(items.Select(i => i.Started)).WaitAsync(TimeSpan.FromSeconds(5));
		items.Reverse().ToList().ForEach(i => i.Complete());

		var results = await consume.WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(results).IsEquivalentTo(items.Select(i => i.Value), CollectionOrdering.Matching);
	}

	[Test]
	public async Task SelectAwait_Concurrent_YieldsInSourceOrderBeyondConcurrencyLimit()
	{
		var items = Enumerable.Range(1, 5).Select(v => new GatedItem(v)).ToArray();
		var pipeline = AsyncSequence.Of(items).SelectAwait(item => item.InvokeAsync(), maxConcurrency: 2);
		var consume = pipeline.ToListAsync().AsTask();
		items.Reverse().ToList().ForEach(i => i.Complete());

		var results = await consume.WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(results).IsEquivalentTo(items.Select(i => i.Value), CollectionOrdering.Matching);
	}

	[Test]
	public async Task SelectAwait_Concurrent_KeepsWindowFullEvenWhenItemBlocksForever()
	{
		var blocker = new GatedItem(1);
		var items = new[] { blocker }.Concat(Enumerable.Range(2, 19).Select(v => new GatedItem(v))).ToArray();
		items.Skip(1).ToList().ForEach(i => i.Complete());

		var pipeline = AsyncSequence.Of(items).SelectAwait(item => item == blocker ? item.InvokeForeverAsync() : item.InvokeAsync(), maxConcurrency: 5);

		// iterator never completes (blocker holds its slot forever).
		_ = pipeline.ToListAsync().AsTask();

		await Task.WhenAll(items.Select(i => i.Started)).WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(items.All(i => i.Started.IsCompletedSuccessfully)).IsTrue();
	}

	[Test]
	public async Task SelectAwait_Concurrent_PreservesOrderUnderChaoticCompletionOrder()
	{
		var items = Enumerable.Range(0, 200).Select(v => new GatedItem(v)).ToArray();
		var pipeline = AsyncSequence.Of(items).SelectAwait(item => item.InvokeAsync(), maxConcurrency: 20);
		var consume = pipeline.ToListAsync().AsTask();

		var random = new Random(0xC0FFEE);
		items.OrderBy(_ => random.Next()).ToList().ForEach(i => i.Complete());

		var results = await consume.WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(results).IsEquivalentTo(items.Select(i => i.Value), CollectionOrdering.Matching);
	}

	[Test]
	public async Task SelectAwait_Concurrent_NeverExceedsConcurrencyLimit()
	{
		var tracker = new ConcurrencyTracker();

		async ValueTask<int> Track(int value)
		{
			using var _ = tracker.Enter();
			await Task.Delay(50);
			return value;
		}

		var pipeline = AsyncSequence.Of(Enumerable.Range(1, 100).ToArray()).SelectAwait(Track, maxConcurrency: 10);

		var results = await pipeline.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(100);
		await Assert.That(tracker.Max).IsEqualTo(10);
	}

	[Test]
	public async Task SelectAwait_WhenConsumerStopsEarly_StopsPullingTheSource()
	{
		var pulled = 0;
		var gate = new TaskCompletionSource();
		var source = Counting(1000, () => Interlocked.Increment(ref pulled));

		await foreach (var _ in source.SelectAwait(async (value, token) => await Gated(value, gate.Task, token), maxConcurrency: 4))
		{
			break;
		}

		var afterBreak = Volatile.Read(ref pulled);
		gate.SetResult();
		await Task.Delay(TimeSpan.FromMilliseconds(200));

		await Assert.That(Volatile.Read(ref pulled)).IsEqualTo(afterBreak);
		await Assert.That(afterBreak).IsLessThan(1000);
	}

	[Test]
	public async Task SelectAwait_WhenConsumerStopsEarly_CancelsAndAwaitsRunningSelectors()
	{
		var waiting = new TaskCompletionSource();
		var cancelled = new TaskCompletionSource();

		async ValueTask<int> Hold(int value, CancellationToken token)
		{
			if (value == 0)
			{
				return value;
			}

			waiting.TrySetResult();
			await Task.Delay(Timeout.Infinite, token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
			cancelled.TrySetResult();
			token.ThrowIfCancellationRequested();
			return value;
		}

		async Task ConsumeAsync()
		{
			await foreach (var _ in AsyncSequence.Of(0, 1, 2).SelectAwait(Hold, maxConcurrency: 3))
			{
				await waiting.Task;
				break;
			}
		}

		await ConsumeAsync().WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(cancelled.Task.IsCompleted).IsTrue();
	}

	[Test]
	public async Task SelectAwait_WhenASelectorThrows_ThrowsAfterTheResultsBeforeIt()
	{
		var seen = new List<int>();

		var act = async () =>
		{
			await foreach (
				var item in AsyncSequence
					.Of(0, 1, 2, 3)
					.SelectAwait(
						(value, _) => value == 2 ? ValueTask.FromException<int>(new InvalidOperationException("selector failed")) : ValueTask.FromResult(value),
						maxConcurrency: 3
					)
			)
			{
				seen.Add(item);
			}
		};

		await Assert.That(act).Throws<InvalidOperationException>().WithMessageContaining("selector failed");
		await Assert.That(seen).IsEquivalentTo([0, 1], CollectionOrdering.Matching);
	}

	[Test]
	public async Task SelectAwait_WhenTheTokenIsCancelled_ThrowsAndCancelsRunningSelectors()
	{
		using var cancellation = new CancellationTokenSource();
		var waiting = new TaskCompletionSource();
		var cancelled = new TaskCompletionSource();

		async ValueTask<int> Hold(int value, CancellationToken token)
		{
			waiting.TrySetResult();
			await Task.Delay(Timeout.Infinite, token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
			cancelled.TrySetResult();
			token.ThrowIfCancellationRequested();
			return value;
		}

		var consume = AsyncSequence.Of(0, 1).SelectAwait(Hold, maxConcurrency: 2, cancellation.Token).ToListAsync().AsTask();
		await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await cancellation.CancelAsync();

		await Assert.That(async () => await consume.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
		await Assert.That(cancelled.Task.IsCompleted).IsTrue();
	}

	[Test]
	public async Task SelectAwait_WhenSourceThrowsAfterItems_YieldsThemThenThrows()
	{
		var seen = new List<int>();

		var act = async () =>
		{
			await foreach (var item in ThrowsAfter(3).SelectAwait((value, _) => ValueTask.FromResult(value), maxConcurrency: 2))
			{
				seen.Add(item);
			}
		};

		await Assert.That(act).Throws<InvalidOperationException>().WithMessageContaining("source failed");
		await Assert.That(seen).IsEquivalentTo([0, 1, 2], CollectionOrdering.Matching);
	}

	private static async Task<int> Gated(int value, Task gate, CancellationToken token)
	{
		if (value > 0)
		{
			await gate.WaitAsync(token);
		}

		return value;
	}

	private sealed record GatedItem(int Value)
	{
		private readonly TaskCompletionSource _started = new();
		private readonly TaskCompletionSource<int> _completion = new();

		public Task Started => _started.Task;

		public ValueTask<int> InvokeAsync()
		{
			_started.TrySetResult();
			return new ValueTask<int>(_completion.Task);
		}

		public ValueTask<int> InvokeForeverAsync()
		{
			_started.TrySetResult();
			return new ValueTask<int>(new TaskCompletionSource<int>().Task);
		}

		public void Complete() => _completion.TrySetResult(Value);
	}

	private sealed class ConcurrencyTracker : IDisposable
	{
		private int _current;
		public int Max { get; private set; }

		public ConcurrencyTracker Enter()
		{
			lock (this)
			{
				if (++_current > Max)
				{
					Max = _current;
				}
			}
			return this;
		}

		public void Dispose()
		{
			lock (this)
			{
				_current--;
			}
		}
	}
}
