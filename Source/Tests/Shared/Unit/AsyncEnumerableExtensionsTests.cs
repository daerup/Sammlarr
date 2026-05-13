using System.Diagnostics;

namespace Shared.Tests.Unit;

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
		var projectedValues = new List<int>();
		var testee = source.SelectAwait(async value =>
		{
			projectedValues.Add(value);
			await Task.Yield();
			return value * 2;
		});

		var results = await testee.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(3);
		await Assert.That(results[0]).IsEqualTo(2);
		await Assert.That(results[1]).IsEqualTo(4);
		await Assert.That(results[2]).IsEqualTo(6);
		await Assert.That(projectedValues.Count).IsEqualTo(3);
		await Assert.That(projectedValues[0]).IsEqualTo(1);
		await Assert.That(projectedValues[1]).IsEqualTo(2);
		await Assert.That(projectedValues[2]).IsEqualTo(3);
	}

	[Test]
	public async Task SelectAwait_Concurrent_RunsItemsUpToConcurrencyLimitInParallel()
	{
		var entered = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource()).ToList();
		var release = new TaskCompletionSource();

		async ValueTask<int> Predicate(int value)
		{
			entered[value - 1].SetResult();
			await release.Task;
			return value * 10;
		}

		var source = CreateAsyncEnumerable(1, 2, 3, 4);
		var task = source.SelectAwait(Predicate, maxConcurrency: 4).ToListAsync().AsTask();

		await Task.WhenAll(entered.Select(t => t.Task)).WaitAsync(TimeSpan.FromSeconds(5));
		release.SetResult();

		var results = await task;
		await Assert.That(results.Count).IsEqualTo(4);
	}

	[Test]
	public async Task SelectAwait_Concurrent_PreservesSourceOrderEvenWhenLaterItemsCompleteFirst()
	{
		var source = CreateAsyncEnumerable(1, 2, 3, 4, 5);

		var testee = source.SelectAwait(
			async value =>
			{
				await Task.Delay((6 - value) * 20);
				return value * 10;
			},
			maxConcurrency: 5
		);

		var results = await testee.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(5);
		await Assert.That(results[0]).IsEqualTo(10);
		await Assert.That(results[1]).IsEqualTo(20);
		await Assert.That(results[2]).IsEqualTo(30);
		await Assert.That(results[3]).IsEqualTo(40);
		await Assert.That(results[4]).IsEqualTo(50);
	}

	[Test]
	public async Task SelectAwait_Concurrent_YieldsInSourceOrderBeyondConcurrencyLimit()
	{
		var source = CreateAsyncEnumerable(1, 2, 3, 4, 5);

		var testee = source.SelectAwait(
			async value =>
			{
				await Task.Delay(value % 2 == 0 ? 1 : 30);
				return value * 10;
			},
			maxConcurrency: 2
		);

		var results = await testee.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(5);
		await Assert.That(results[0]).IsEqualTo(10);
		await Assert.That(results[1]).IsEqualTo(20);
		await Assert.That(results[2]).IsEqualTo(30);
		await Assert.That(results[3]).IsEqualTo(40);
		await Assert.That(results[4]).IsEqualTo(50);
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
	public async Task SelectAwait_Concurrent_PreservesAnchorActivityAcrossYieldsRegardlessOfConsumerContext()
	{
		using var source = new ActivitySource("Shared.Tests.AsyncEnumerable");
		using var listener = new ActivityListener
		{
			ShouldListenTo = s => s.Name == "Shared.Tests.AsyncEnumerable",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var anchor = source.StartActivity("anchor")!;
		var observedParentIds = new List<string?>();

		var pipeline = CreateAsyncEnumerable(1, 2, 3, 4, 5)
			.SelectAwait(
				async value =>
				{
					await Task.Yield();
					using var child = source.StartActivity($"child-{value}");
					observedParentIds.Add(child?.ParentSpanId.ToString());
					return value;
				},
				maxConcurrency: 2
			);

		await foreach (var _ in pipeline)
		{
			using var consumerNoise = source.StartActivity("consumer-noise");
			await Task.Yield();
		}

		var expectedParent = anchor.SpanId.ToString();
		await Assert.That(observedParentIds.Count).IsEqualTo(5);
		foreach (var observed in observedParentIds)
		{
			await Assert.That(observed).IsEqualTo(expectedParent);
		}
	}

	private static async IAsyncEnumerable<int> CreateAsyncEnumerable(params int[] values)
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
}
