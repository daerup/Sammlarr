using System.Net;
using System.Net.Http.Headers;
using API.Http;
using API.Notifications.Webhook;
using Contracts.Identity;
using Contracts.Providers;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

		using var response = await SendThrottledAsync(host, Stamped());

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		reporter.ReportAsync(MediaProvider.Plex, Any(), user, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddProviderHttp_WhenTheRequestHasNoUser_ReportsNothing()
	{
		var reporter = IProviderRateLimitReporter.Mock();
		using var host = Host(reporter.Object);

		using var response = await SendThrottledAsync(host, new HttpRequestMessage(HttpMethod.Get, "https://plex.test/library"));

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		reporter.ReportAsync(Any<MediaProvider>(), Any(), Any<UserId>(), Any()).WasCalled(Times.Never);
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

	private static async Task<HttpResponseMessage> SendThrottledAsync(IHost host, HttpRequestMessage request)
	{
		var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("plex");
		return await client.SendAsync(request);
	}

	private static IHost Host(IProviderRateLimitReporter reporter) =>
		new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(
					new PlexOptions
					{
						BaseUrl = "https://plex.test",
						Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
						Http = new ProviderHttpOptions { RetryAttempts = 1 },
					}
				);
				services.AddSingleton<ProviderConcurrencyGates>();
				services.AddSingleton(reporter);
				services.AddHttpClient("plex").AddProviderHttp<PlexOptions>(MediaProvider.Plex).ConfigurePrimaryHttpMessageHandler(() => new ThrottlingHandler());
			})
			.Build();

	private sealed class ThrottlingHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
			{
				RequestMessage = request,
				Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) },
			};
			return Task.FromResult(response);
		}
	}
}
