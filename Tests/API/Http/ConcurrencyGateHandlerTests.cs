using System.Net;
using API.Http;

namespace API.Tests.Http;

public class ConcurrencyGateHandlerTests
{
	[Test]
	public async Task SendAsync_WithGate_CapsConcurrentRequests_AtTheGateLimit()
	{
		var tracker = new ConcurrencyTrackingHandler(TimeSpan.FromMilliseconds(40));
		var gate = new SemaphoreSlim(25, 25);
		using var invoker = new HttpMessageInvoker(new ConcurrencyGateHandler(gate) { InnerHandler = tracker });

		var tasks = Enumerable
			.Range(0, 100)
			.Select(_ => invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://plex.test/library/metadata/1"), CancellationToken.None))
			.ToList();
		await Task.WhenAll(tasks);

		await Assert.That(tracker.MaxObserved).IsLessThanOrEqualTo(25);
	}

	[Test]
	public async Task SendAsync_WithoutGate_DoesNotCapConcurrency()
	{
		var tracker = new ConcurrencyTrackingHandler(TimeSpan.FromMilliseconds(40));
		using var invoker = new HttpMessageInvoker(new ConcurrencyGateHandler(null) { InnerHandler = tracker });

		var tasks = Enumerable
			.Range(0, 50)
			.Select(_ => invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://plex.test/library/metadata/1"), CancellationToken.None))
			.ToList();
		await Task.WhenAll(tasks);

		await Assert.That(tracker.MaxObserved).IsGreaterThan(25);
	}

	private sealed class ConcurrencyTrackingHandler(TimeSpan delay) : HttpMessageHandler
	{
		private readonly object _sync = new();
		private int _current;

		public int MaxObserved { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			lock (_sync)
			{
				_current++;
				if (_current > MaxObserved)
				{
					MaxObserved = _current;
				}
			}

			try
			{
				await Task.Delay(delay, cancellationToken);
			}
			finally
			{
				lock (_sync)
				{
					_current--;
				}
			}

			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
		}
	}
}
