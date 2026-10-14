using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using API.Http.Lanes;
using Domain.Providers;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace API.Tests.Http;

public class LaneHandlerTests
{
	[Test]
	public async Task SendAsync_RetryingA503_TakesAPermitPerAttempt()
	{
		var wire = new Scripted(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
		var http = new ProviderHttpOptions
		{
			RetryAttempts = 1,
			Lanes = new() { ["api"] = Window(1, TimeSpan.FromSeconds(1)) },
		};
		using var host = LanedHosts.Host(LanedHosts.Plex(http), RemoteService.Plex, wire);

		using var response = await host.Client().GetAsync("https://plex.test/library");

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(wire.Gap()).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900));
	}

	[Test]
	public async Task SendAsync_On429WithRetryAfter_BlocksTheRouteForEveryClient()
	{
		var wire = new Scripted(HttpStatusCode.TooManyRequests);
		using var host = LanedHosts.Host(LanedHosts.Plex(new ProviderHttpOptions { RetryAttempts = 0 }), RemoteService.Plex, wire, clients: ["plex", "plex-sink"]);
		using var throttled = await host.Client("plex").GetAsync("https://plex.test/library");

		await Assert.That(async () => await host.Client("plex-sink").GetAsync("https://plex.test/api/v2/user")).Throws<LaneRefusedException>();
		await Assert.That(wire.Asked).IsEqualTo(1);
	}

	[Test]
	public async Task SendAsync_OnAPlexMetadata429_LeavesTheWatchlistRouteOpen()
	{
		var wire = new Scripted(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
		var http = new ProviderHttpOptions
		{
			RetryAttempts = 0,
			Lanes = new() { ["metadata"] = Routed("metadata", "library/metadata"), ["watchlist"] = Routed("discover", "library/sections/watchlist") },
		};
		using var host = LanedHosts.Host(LanedHosts.Plex(http), RemoteService.Plex, wire);
		using var throttled = await host.Client().GetAsync("https://metadata.plex.test/library/metadata/5d776be17a53e9001e732ab9");

		using var watchlist = await host.Client().GetAsync("https://discover.plex.test/library/sections/watchlist/all");

		await Assert.That(watchlist.StatusCode).IsEqualTo(HttpStatusCode.OK);
	}

	[Test]
	public async Task SendAsync_On429BeyondTheMaxWait_HandsBackThe429WithoutRetrying()
	{
		var wire = new Scripted(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
		using var host = LanedHosts.Host(LanedHosts.Plex(new ProviderHttpOptions { RetryAttempts = 2, MaxWaitSeconds = 5 }), RemoteService.Plex, wire);

		using var response = await host.Client().GetAsync("https://plex.test/library");

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		await Assert.That(wire.Asked).IsEqualTo(1);
	}

	[Test]
	public async Task SendAsync_ForTwoMdblistKeys_KeepsTheirWindowsApart()
	{
		var wire = new Scripted(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK);
		var options = new MdblistOptions
		{
			BaseUrl = "https://api.mdblist.test",
			Http = new ProviderHttpOptions { Lanes = new() { ["api"] = Window(1, TimeSpan.FromMinutes(5)) } },
		};
		using var host = LanedHosts.Host(options, RemoteService.Mdblist, wire);

		using var first = await host.Client().SendAsync(Owned("first-key"));
		using var second = await host.Client().SendAsync(Owned("second-key")).WaitAsync(TimeSpan.FromSeconds(5));
		using var stop = new CancellationTokenSource();
		var third = host.Client().SendAsync(Owned("first-key"), stop.Token);

		await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(async () => await third.WaitAsync(TimeSpan.FromMilliseconds(300))).Throws<TimeoutException>();
		await stop.CancelAsync();
		await Assert.That(async () => await third).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task SendAsync_On429_WarnsWithTheLaneAndTheResponseHeaders()
	{
		var logger = ILogger<LaneHandler>.Mock();
		var wire = new Scripted(HttpStatusCode.TooManyRequests);
		var http = new ProviderHttpOptions
		{
			RetryAttempts = 0,
			Lanes = new() { ["match"] = Routed("discover", "library/metadata/matches") },
		};
		using var host = LanedHosts.Host(LanedHosts.Plex(http), RemoteService.Plex, wire, logger: logger.Object);

		using var throttled = await host.Client().GetAsync("https://discover.plex.test/library/metadata/matches?type=1&guid=tmdb%3A%2F%2F550");

		await Assert.That(Warnings(logger).Single()).Contains("Plex answered 429 on its match lane").And.Contains("Retry-After: 60");
	}

	private static IEnumerable<string> Warnings(Mock<ILogger<LaneHandler>> logger)
	{
		return Mock.Invocations(logger).Where(call => call.Arguments[0] is LogLevel.Warning).Select(call => $"{call.Arguments[2]}");
	}

	private static LaneOptions Window(int requests, TimeSpan per) => new() { Limits = [new RateWindowOptions { Requests = requests, Per = per }] };

	private static LaneOptions Routed(string url, string path) => new() { Routes = [new LaneRouteOptions { Url = url, Path = path }] };

	private static HttpRequestMessage Owned(string apiKey)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "https://api.mdblist.test/lists/1/items");
		request.Options.Set(LaneHandler.Owner, apiKey);
		return request;
	}

	private sealed class Scripted(params HttpStatusCode[] answers) : HttpMessageHandler
	{
		private readonly ConcurrentQueue<HttpStatusCode> _answers = new(answers);
		private readonly ConcurrentQueue<long> _asked = new();

		public int Asked => _asked.Count;

		public TimeSpan Gap() => Stopwatch.GetElapsedTime(_asked.First(), _asked.Last());

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_asked.Enqueue(Stopwatch.GetTimestamp());
			var answer = _answers.TryDequeue(out var next) ? next : HttpStatusCode.OK;
			var response = new HttpResponseMessage(answer) { RequestMessage = request };
			if (answer is HttpStatusCode.TooManyRequests)
			{
				response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
			}

			return Task.FromResult(response);
		}
	}
}
