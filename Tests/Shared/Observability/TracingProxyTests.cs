using System.Diagnostics;
using Observability.Tracing;

namespace Shared.Tests.Observability;

public class TracingProxyTests
{
	private interface IThing
	{
		int Sync(int value);
		Task<string> DoAsync(string value);
		IAsyncEnumerable<int> Stream();
	}

	private sealed class Thing : IThing
	{
		public int Sync(int value) => value * 2;

		public async Task<string> DoAsync(string value)
		{
			await Task.Delay(1);
			return value + "!";
		}

		public async IAsyncEnumerable<int> Stream()
		{
			for (var i = 0; i < 3; i++)
			{
				await Task.Delay(1);
				yield return i;
			}
		}
	}

	[Test]
	public async Task Proxy_CreatesOneActivityPerCall_AcrossSyncTaskAndStream()
	{
		var stopped = new List<Activity>();
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == Instrumentation.ServiceName,
			Sample = (ref _) => ActivitySamplingResult.AllData,
			ActivityStopped = stopped.Add,
		};
		ActivitySource.AddActivityListener(listener);

		var proxy = TracingProxy<IThing>.Wrap(new Thing());

		var sync = proxy.Sync(21);
		var done = await proxy.DoAsync("hi");
		var streamed = await proxy.Stream().ToListAsync();

		await Assert.That(sync).IsEqualTo(42);
		await Assert.That(done).IsEqualTo("hi!");
		await Assert.That(streamed).IsEquivalentTo([0, 1, 2]);

		var names = stopped.Select(activity => activity.OperationName).ToList();
		await Assert.That(names).Contains("IThing.Sync");
		await Assert.That(names).Contains("IThing.DoAsync");
		await Assert.That(names).Contains("IThing.Stream");
		await Assert.That(stopped.All(activity => activity.Duration >= TimeSpan.Zero)).IsTrue();
	}
}
