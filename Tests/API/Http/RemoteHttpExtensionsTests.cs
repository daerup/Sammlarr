using System.Net;
using System.Net.Http.Headers;
using API.Http;
using API.Notifications.Webhook;
using Contracts;
using Contracts.Identity;
using Contracts.Providers;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly.CircuitBreaker;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Http;

public class RemoteHttpExtensionsTests
{
	private static readonly UserId user = UserId.New();

	[Test]
	public async Task AddClientHttp_WithZeroRetries_PassesStartupOptionsValidation()
	{
		using var host = new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(new WebhookOptions());
				services.AddSingleton<ProviderGates>();
				services.AddHttpClient<WebhookClient>().AddClientHttp<WebhookOptions>();
			})
			.Build();

		await host.StartAsync();
		await host.StopAsync();
	}

	[Test]
	public async Task AddRemoteHttp_WhenAStampedRequestIsThrottled_ReportsTheRateLimit()
	{
		var reporter = IProviderRateLimitReporter.Mock();
		using var host = Host(reporter.Object);

		using var response = await SendAsync(host, Stamped());

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		reporter.ReportAsync(RemoteService.Plex, Any(), user, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddRemoteHttp_WhenTheRequestHasNoUser_ReportsNothing()
	{
		var reporter = IProviderRateLimitReporter.Mock();
		using var host = Host(reporter.Object);

		using var response = await SendAsync(host, new HttpRequestMessage(HttpMethod.Get, "https://plex.test/library"));

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		reporter.ReportAsync(Any<RemoteService>(), Any(), Any<UserId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task AddRemoteHttp_WhenThrottled_BlocksTheRemoteServiceInItsCapacity()
	{
		using var host = Host(IProviderRateLimitReporter.Mock().Object);

		using var response = await SendAsync(host, Stamped());

		await Assert.That(host.Services.GetRequiredService<RemoteCapacity>().Of(RemoteService.Plex).BlockedUntil).IsSome();
	}

	[Test]
	public async Task AddRemoteHttp_WhenTheCircuitBreakerOpens_BlocksTheRemoteServiceInItsCapacity()
	{
		using var host = Host(IProviderRateLimitReporter.Mock().Object, HttpStatusCode.ServiceUnavailable, minimumThroughput: 2);
		using var failed = await SendAsync(host, Stamped());

		await Assert.That(async () => await SendAsync(host, Stamped())).Throws<BrokenCircuitException>();
		await Assert.That(host.Services.GetRequiredService<RemoteCapacity>().Of(RemoteService.Plex).BlockedUntil).IsSome();
	}

	[Test]
	public async Task AddRemoteHttp_ForARemoteServiceWithWaitBudgets_ReportsBothInItsCapacity()
	{
		using var host = Host(IProviderRateLimitReporter.Mock().Object, maxWaitSeconds: 90, enrichWaitSeconds: 15);

		var load = host.Services.GetRequiredService<RemoteCapacity>().Of(RemoteService.Plex);

		await Assert.That(load.MaxWait).IsEqualTo(TimeSpan.FromSeconds(90));
		await Assert.That(load.EnrichWait).IsEqualTo(TimeSpan.FromSeconds(15));
	}

	[Test]
	public async Task AddRemoteHttp_WithARequestRate_HoldsTheRequestsBeyondItForTheNextWindow()
	{
		var handler = new AnsweringHandler(HttpStatusCode.OK);
		using var host = Host(IProviderRateLimitReporter.Mock().Object, handler, requestsPerSecond: 2);

		var sends = Enumerable.Range(0, 3).Select(_ => SendAsync(host, new HttpRequestMessage(HttpMethod.Get, "https://plex.test/library"))).ToList();

		await Assert.That(async () => await Task.WhenAll(sends).WaitAsync(TimeSpan.FromMilliseconds(500))).Throws<TimeoutException>();
		await Assert.That(handler.Answered).IsEqualTo(2);
		await Task.WhenAll(sends);
		await Assert.That(handler.Answered).IsEqualTo(3);
	}

	private static HttpRequestMessage Stamped()
	{
		var currentUser = ICurrentUser.Mock();
		currentUser.IsAuthenticated.Returns(true);
		currentUser.Id.Returns(user);

		var request = new HttpRequestMessage(HttpMethod.Get, "https://plex.test/library");
		request.StampTriggeredBy(currentUser.Object);
		return request;
	}

	private static async Task<HttpResponseMessage> SendAsync(IHost host, HttpRequestMessage request)
	{
		var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("plex");
		return await client.SendAsync(request);
	}

	private static IHost Host(
		IProviderRateLimitReporter reporter,
		HttpStatusCode answer = HttpStatusCode.TooManyRequests,
		int minimumThroughput = 20,
		int maxWaitSeconds = 0,
		int enrichWaitSeconds = 0
	)
	{
		return Host(reporter, new AnsweringHandler(answer), minimumThroughput, maxWaitSeconds, enrichWaitSeconds);
	}

	private static IHost Host(
		IProviderRateLimitReporter reporter,
		AnsweringHandler handler,
		int minimumThroughput = 20,
		int maxWaitSeconds = 0,
		int enrichWaitSeconds = 0,
		int requestsPerSecond = 0
	)
	{
		return new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(
					new PlexOptions
					{
						BaseUrl = "https://plex.test",
						Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
						Http = new ProviderHttpOptions
						{
							RetryAttempts = 1,
							CircuitBreakerMinimumThroughput = minimumThroughput,
							MaxWaitSeconds = maxWaitSeconds,
							EnrichWaitSeconds = enrichWaitSeconds,
							RequestsPerSecond = requestsPerSecond,
						},
					}
				);
				services.AddSingleton<ProviderGates>();
				services.AddSingleton<RemoteCapacity>();
				services.AddSingleton(IDateTimeProvider.Mock().Object);
				services.AddSingleton(reporter);
				services.AddHttpClient("plex").AddRemoteHttp<PlexOptions>(RemoteService.Plex).ConfigurePrimaryHttpMessageHandler(() => handler);
			})
			.Build();
	}

	private sealed class AnsweringHandler(HttpStatusCode answer) : HttpMessageHandler
	{
		private int _answered;

		public int Answered => _answered;

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _answered);
			var response = new HttpResponseMessage(answer)
			{
				RequestMessage = request,
				Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
			};
			return Task.FromResult(response);
		}
	}
}
