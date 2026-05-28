using TUnit.Assertions.Enums;

namespace Shared.Tests;

public class AsyncEnumerableExtensionsTests
{
	[Test]
	public async Task SelectAwait_WhenSequenceIsNotEnumerated_DoesNotInvokeProjection()
	{
		var source = CreateAsyncEnumerable(1, 2, 3);
		var wasInvoked = false;
		var testee = source.SelectAwait(value =>
		{
			wasInvoked = true;
			return new ValueTask<int>(value * 2);
		});

		await Assert.That(wasInvoked).IsFalse();
		await Assert.That(testee).IsTypeOf<IAsyncEnumerator<int>>();
	}

	[Test]
	public async Task SelectAwait_WhenSourceIsEnumerated_ProjectsValuesInOrder()
	{
		var source = CreateAsyncEnumerable(1, 2, 3);
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
		var pipeline = CreateAsyncEnumerable(items).SelectAwait(item => item.InvokeAsync(), maxConcurrency: 4);
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
		var pipeline = CreateAsyncEnumerable(items).SelectAwait(item => item.InvokeAsync(), maxConcurrency: 5);
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
		var pipeline = CreateAsyncEnumerable(items).SelectAwait(item => item.InvokeAsync(), maxConcurrency: 2);
		var consume = pipeline.ToListAsync().AsTask();
		items.Reverse().ToList().ForEach(i => i.Complete());

		var results = await consume.WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(results).IsEquivalentTo(items.Select(i => i.Value), CollectionOrdering.Matching);
	}

	[Test]
	public async Task SelectAwait_WhenProjectionThrows_PropagatesException()
	{
		var source = CreateAsyncEnumerable(() => 1, () => 2, () => throw new InvalidOperationException());
		var projectedValues = new List<int>();
		var testee = source.SelectAwait(value =>
		{
			projectedValues.Add(value);
			return new ValueTask<int>(value);
		});

		await Assert.That(async () => await testee.ToListAsync()).Throws<InvalidOperationException>();
		await Assert.That(projectedValues.Count).IsEqualTo(2);
		await Assert.That(projectedValues[0]).IsEqualTo(1);
		await Assert.That(projectedValues[1]).IsEqualTo(2);
	}

	[Test]
	public async Task SelectAwait_Concurrent_KeepsWindowFullEvenWhenItemBlocksForever()
	{
		var blocker = new GatedItem(1);
		var items = new[] { blocker }.Concat(Enumerable.Range(2, 19).Select(v => new GatedItem(v))).ToArray();
		items.Skip(1).ToList().ForEach(i => i.Complete());

		var pipeline = CreateAsyncEnumerable(items).SelectAwait(item => item == blocker ? item.InvokeForeverAsync() : item.InvokeAsync(), maxConcurrency: 5);

		// iterator never completes (blocker holds its slot forever).
		_ = pipeline.ToListAsync().AsTask();

		await Task.WhenAll(items.Select(i => i.Started)).WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(items.All(i => i.Started.IsCompletedSuccessfully)).IsTrue();
	}

	[Test]
	public async Task SelectAwait_Concurrent_PreservesOrderUnderChaoticCompletionOrder()
	{
		var items = Enumerable.Range(0, 200).Select(v => new GatedItem(v)).ToArray();
		var pipeline = CreateAsyncEnumerable(items).SelectAwait(item => item.InvokeAsync(), maxConcurrency: 20);
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

		var pipeline = CreateAsyncEnumerable(Enumerable.Range(1, 100).ToArray()).SelectAwait(Track, maxConcurrency: 10);

		var results = await pipeline.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(100);
		await Assert.That(tracker.Max).IsEqualTo(10);
	}

	private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(params IEnumerable<T> values)
	{
		foreach (var value in values)
		{
			await Task.Yield();
			yield return value;
		}
	}

	private static async IAsyncEnumerable<int> CreateAsyncEnumerable(params Func<int>[] valueProviders)
	{
		foreach (var valueProvider in valueProviders)
		{
			await Task.Yield();
			yield return valueProvider();
		}
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
