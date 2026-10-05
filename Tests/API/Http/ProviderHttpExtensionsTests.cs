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

public class ProviderHttpExtensionsTests
{
	private static readonly UserId user = UserId.New();

	[Test]
	public async Task AddProviderHttp_WithZeroRetries_PassesStartupOptionsValidation()
	{
		using var host = new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(new WebhookOptions());
				services.AddSingleton<ProviderConcurrencyGates>();
				services.AddHttpClient<WebhookClient>().AddProviderHttp<WebhookOptions>();
			})
			.Build();

		await host.StartAsync();
		await host.StopAsync();
	}

	[Test]
	public async Task AddProviderHttp_WhenAStampedRequestIsThrottled_ReportsTheRateLimit()
	{
		var reporter = IProviderRateLimitReporter.Mock();
		using var host = Host(reporter.Object);

		using var response = await SendAsync(host, Stamped());

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		reporter.ReportAsync(MediaProvider.Plex, Any(), user, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddProviderHttp_WhenTheRequestHasNoUser_ReportsNothing()
	{
		var reporter = IProviderRateLimitReporter.Mock();
		using var host = Host(reporter.Object);

		using var response = await SendAsync(host, new HttpRequestMessage(HttpMethod.Get, "https://plex.test/library"));

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		reporter.ReportAsync(Any<MediaProvider>(), Any(), Any<UserId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task AddProviderHttp_WhenThrottled_BlocksTheProviderInItsCapacity()
	{
		using var host = Host(IProviderRateLimitReporter.Mock().Object);

		using var response = await SendAsync(host, Stamped());

		await Assert.That(host.Services.GetRequiredService<ProviderCapacity>().Of(MediaProvider.Plex).BlockedUntil).IsSome();
	}

	[Test]
	public async Task AddProviderHttp_WhenTheCircuitBreakerOpens_BlocksTheProviderInItsCapacity()
	{
		using var host = Host(IProviderRateLimitReporter.Mock().Object, HttpStatusCode.ServiceUnavailable, minimumThroughput: 2);
		using var failed = await SendAsync(host, Stamped());

		await Assert.That(async () => await SendAsync(host, Stamped())).Throws<BrokenCircuitException>();
		await Assert.That(host.Services.GetRequiredService<ProviderCapacity>().Of(MediaProvider.Plex).BlockedUntil).IsSome();
	}

	[Test]
	public async Task AddProviderHttp_ForAProviderWithWaitBudgets_ReportsBothInItsCapacity()
	{
		using var host = Host(IProviderRateLimitReporter.Mock().Object, maxWaitSeconds: 90, enrichWaitSeconds: 15);

		var load = host.Services.GetRequiredService<ProviderCapacity>().Of(MediaProvider.Plex);

		await Assert.That(load.MaxWait).IsEqualTo(TimeSpan.FromSeconds(90));
		await Assert.That(load.EnrichWait).IsEqualTo(TimeSpan.FromSeconds(15));
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
						},
					}
				);
				services.AddSingleton<ProviderConcurrencyGates>();
				services.AddSingleton<ProviderCapacity>();
				services.AddSingleton(IDateTimeProvider.Mock().Object);
				services.AddSingleton(reporter);
				services.AddHttpClient("plex").AddProviderHttp<PlexOptions>(MediaProvider.Plex).ConfigurePrimaryHttpMessageHandler(() => new AnsweringHandler(answer));
			})
			.Build();
	}

	private sealed class AnsweringHandler(HttpStatusCode answer) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(answer)
			{
				RequestMessage = request,
				Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
			};
			return Task.FromResult(response);
		}
	}
}
