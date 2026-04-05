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
