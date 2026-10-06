using System.Net;
using System.Net.Http.Headers;
using API.Http;
using Contracts;
using Domain.Providers;

namespace API.Tests.Http;

public class ProviderCapacityTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

	private readonly ProviderGates _gates = new();
	private readonly ProviderCapacity Testee;

	public ProviderCapacityTests()
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(Now);
		Testee = new ProviderCapacity(_gates, clock.Object, []);
	}

	[Test]
	public async Task Of_AfterATooManyRequestsAnswer_StaysBlockedForTheRetryAfterDelay()
	{
		var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
		response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(20));

		await SendAsync(response);

		var load = Testee.Of(MediaProvider.Tmdb);
		await Assert.That(load.IsOpen(Now)).IsFalse();
		await Assert.That(load.IsOpen(Now.AddSeconds(20))).IsTrue();
	}

	[Test]
	public async Task Of_AfterATooManyRequestsAnswerWithoutADelay_StaysBlockedForHalfAMinute()
	{
		await SendAsync(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

		var load = Testee.Of(MediaProvider.Tmdb);
		await Assert.That(load.IsOpen(Now.AddSeconds(29))).IsFalse();
		await Assert.That(load.IsOpen(Now.AddSeconds(30))).IsTrue();
	}

	[Test]
	public async Task Of_AfterATooManyRequestsAnswerThatSaysRetryNow_IsOpen()
	{
		var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
		response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);

		await SendAsync(response);

		await Assert.That(Testee.Of(MediaProvider.Tmdb).IsOpen(Now)).IsTrue();
	}

	[Test]
	public async Task Of_AfterASuccessfulAnswer_IsOpen()
	{
		await SendAsync(new HttpResponseMessage(HttpStatusCode.OK));

		await Assert.That(Testee.Of(MediaProvider.Tmdb).IsOpen(Now)).IsTrue();
	}

	[Test]
	public async Task Of_WhileHalfTheGateIsInUse_ReportsTheProviderHalfBusy()
	{
		var gate = _gates.Concurrency("tmdb", 4, Some.Of(MediaProvider.Tmdb));
		await gate.WaitAsync();
		await gate.WaitAsync();

		await Assert.That(Testee.Of(MediaProvider.Tmdb).Busy).IsEqualTo(0.5);
		await Assert.That(Testee.Of(MediaProvider.Plex).Busy).IsEqualTo(0);
	}

	[Test]
	public async Task Of_WhileAGateWithoutAProviderIsInUse_CountsItForNoProvider()
	{
		var gate = _gates.Concurrency("webhook", 2, new None());
		await gate.WaitAsync();

		await Assert.That(Testee.Of(MediaProvider.Tmdb).Busy).IsEqualTo(0);
	}

	[Test]
	public async Task Of_WhileARequestWaitsBehindAUsedUpRateWindow_ReportsTheProviderBusierThanFull()
	{
		var window = _gates.Rate("Tmdb", 2, Some.Of(MediaProvider.Tmdb));
		using var first = await window.AcquireAsync();
		using var second = await window.AcquireAsync();

		var waiting = window.AcquireAsync().AsTask();

		await Assert.That(Testee.Of(MediaProvider.Tmdb).Busy).IsEqualTo(1.5);
		await Assert.That(Testee.Of(MediaProvider.Plex).Busy).IsEqualTo(0);
		using var third = await waiting;
	}

	private async Task SendAsync(HttpResponseMessage response)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWith(response);
		using var invoker = new HttpMessageInvoker(new CapacityHandler(MediaProvider.Tmdb, Testee) { InnerHandler = handler });
		await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.themoviedb.org/3/movie/550"), CancellationToken.None);
	}
}
