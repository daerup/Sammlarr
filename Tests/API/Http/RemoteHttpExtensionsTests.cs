using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using API.Http;
using API.Http.Lanes;
using API.Notifications.Webhook;
using Contracts.Identity;
using Contracts.Providers;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly.Timeout;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Http;

public class RemoteHttpExtensionsTests
{
	private static readonly UserId user = UserId.New();

	[Test]
	public async Task AddClientHttp_WithZeroRetries_PassesStartupOptionsValidation()
	{
		using var host = ClientHost(new WebhookOptions());

		await host.StartAsync();
		await host.StopAsync();
	}

	[Test]
	[MethodDataSource(nameof(ProviderSections))]
	public async Task AddClientHttp_WithEachProvidersShippedSettings_PassesStartupOptionsValidation(string section)
	{
		using var host = ClientHost(new WebhookOptions { Http = ShippedHttp(section) });

		await host.StartAsync();
		await host.StopAsync();
	}

	[Test]
	[MethodDataSource(nameof(LanedSections))]
	public async Task Bind_ForEachProvidersShippedLanes_ReadsEveryLaneAndItsLimits(string section)
	{
		var shipped = AppSettings().GetProperty(section).GetProperty("Http").GetProperty("Lanes");

		var bound = ShippedHttp(section).Lanes;

		await Assert.That(bound.Keys).IsEquivalentTo(shipped.EnumerateObject().Select(lane => lane.Name));
		await Assert.That(bound.Values.SelectMany(lane => lane.Limits).Where(limit => limit.Requests <= 0 || limit.Per <= TimeSpan.Zero)).IsEmpty();
		await Assert.That(bound.Values.SelectMany(lane => lane.Routes).Where(route => string.IsNullOrEmpty(route.Url))).IsEmpty();
	}

	[Test]
	public async Task AddClientHttp_ForPictures_TimesOutAfterItsTimeoutSeconds()
	{
		using var host = ClientHost(
			new PictureOptions
			{
				Http = new ProviderHttpOptions
				{
					RetryAttempts = 3,
					AttemptTimeoutSeconds = 1,
					TimeoutSeconds = 1,
				},
			},
			new Failing()
		);

		await Assert.That(async () => await host.Client("pictures").GetAsync("https://images.test/poster.jpg")).Throws<TimeoutRejectedException>();
	}

	[Test]
	public async Task AddRemoteHttp_ForALaneClient_AllowsThirtyMinutesByDefault()
	{
		using var host = LanedHosts.Host(LanedHosts.Plex(new ProviderHttpOptions()), RemoteService.Plex, new Counting());

		await Assert.That(host.Client().Timeout).IsEqualTo(TimeSpan.FromMinutes(30));
	}

	[Test]
	public async Task AddRemoteHttp_ForALaneClient_TakesTheGlobalTimeout()
	{
		using var host = LanedHosts.Host(LanedHosts.Plex(new ProviderHttpOptions()), RemoteService.Plex, new Counting(), httpTimeout: TimeSpan.FromMinutes(45));

		await Assert.That(host.Client().Timeout).IsEqualTo(TimeSpan.FromMinutes(45));
	}

	[Test]
	public async Task AddRemoteHttp_WhenAStampedRequestIsThrottled_ReportsTheRateLimit()
	{
		var reporter = IProviderRateLimitReporter.Mock();
		using var host = LanedHosts.Host(LanedHosts.Plex(new ProviderHttpOptions { RetryAttempts = 0 }), RemoteService.Plex, new Throttling(), reporter.Object);

		using var response = await host.Client().SendAsync(Stamped());

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		reporter.ReportAsync(RemoteService.Plex, Any(), user, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddRemoteHttp_WhenThrottledWithoutRetryAfter_ReportsTheLanesWindowAsTheWait()
	{
		var reporter = IProviderRateLimitReporter.Mock();
		var http = new ProviderHttpOptions
		{
			RetryAttempts = 0,
			Lanes = new Dictionary<string, LaneOptions>
			{
				["api"] = new LaneOptions { Limits = [new RateWindowOptions { Requests = 10, Per = TimeSpan.FromMinutes(1) }] },
			},
		};
		using var host = LanedHosts.Host(LanedHosts.Plex(http), RemoteService.Plex, new Throttling(retryAfter: false), reporter.Object);

		using var response = await host.Client().SendAsync(Stamped());

		reporter.ReportAsync(RemoteService.Plex, TimeSpan.FromMinutes(1), user, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddRemoteHttp_WhenTheRequestHasNoUser_ReportsNothing()
	{
		var reporter = IProviderRateLimitReporter.Mock();
		using var host = LanedHosts.Host(LanedHosts.Plex(new ProviderHttpOptions { RetryAttempts = 0 }), RemoteService.Plex, new Throttling(), reporter.Object);

		using var response = await host.Client().GetAsync("https://plex.test/library");

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		reporter.ReportAsync(Any<RemoteService>(), Any(), Any<UserId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task AddRemoteHttp_WhenThrottled_BlocksTheRemoteServiceInItsCapacity()
	{
		using var host = LanedHosts.Host(LanedHosts.Plex(new ProviderHttpOptions { RetryAttempts = 0 }), RemoteService.Plex, new Throttling());

		using var response = await host.Client().SendAsync(Stamped());

		await Assert.That(host.Load(RemoteService.Plex).BlockedUntil).IsSome();
	}

	[Test]
	public async Task AddRemoteHttp_WhenTheRouteGoesDown_RefusesTheNextRequestAndBlocksTheRemoteServiceInItsCapacity()
	{
		var http = new ProviderHttpOptions
		{
			RetryAttempts = 0,
			Lanes = new Dictionary<string, LaneOptions> { ["api"] = new LaneOptions { FailuresToDown = 1 } },
		};
		using var host = LanedHosts.Host(LanedHosts.Plex(http), RemoteService.Plex, new Failing());
		using var failed = await host.Client().GetAsync("https://plex.test/library");

		await Assert.That(async () => await host.Client().GetAsync("https://plex.test/library")).Throws<LaneRefusedException>();
		await Assert.That(host.Load(RemoteService.Plex).BlockedUntil).IsSome();
	}

	[Test]
	public async Task AddRemoteHttp_WithAWindow_HoldsTheRequestsBeyondItForTheNextWindow()
	{
		var answering = new Counting();
		var http = new ProviderHttpOptions
		{
			Lanes = new Dictionary<string, LaneOptions>
			{
				["api"] = new LaneOptions { Limits = [new RateWindowOptions { Requests = 2, Per = TimeSpan.FromSeconds(1) }] },
			},
		};
		using var host = LanedHosts.Host(LanedHosts.Plex(http), RemoteService.Plex, answering);

		var sends = Enumerable.Range(0, 3).Select(_ => host.Client().GetAsync("https://plex.test/library")).ToList();

		await Assert.That(async () => await Task.WhenAll(sends).WaitAsync(TimeSpan.FromMilliseconds(500))).Throws<TimeoutException>();
		await Assert.That(answering.Answered).IsEqualTo(2);
		await Task.WhenAll(sends).WaitAsync(TimeSpan.FromSeconds(5));
		await Assert.That(answering.Answered).IsEqualTo(3);
	}

	public static IEnumerable<string> ProviderSections()
	{
		return AppSettings()
			.EnumerateObject()
			.Where(section => section.Value.ValueKind is JsonValueKind.Object && section.Value.TryGetProperty("Http", out _))
			.Select(section => section.Name);
	}

	public static IEnumerable<string> LanedSections()
	{
		return ProviderSections().Where(section => AppSettings().GetProperty(section).GetProperty("Http").TryGetProperty("Lanes", out _));
	}

	private static string AppSettingsPath() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Web", "appsettings.json"));

	private static JsonElement AppSettings() => JsonElement.Parse(File.ReadAllText(AppSettingsPath()));

	private static ProviderHttpOptions ShippedHttp(string section)
	{
		var configuration = new ConfigurationBuilder().AddJsonFile(AppSettingsPath()).Build();
		return configuration.GetSection(section).GetSection("Http").Get<ProviderHttpOptions>() ?? new ProviderHttpOptions();
	}

	private static IHost ClientHost<TOptions>(TOptions options, HttpMessageHandler? wire = null)
		where TOptions : class, IProviderHttpOptions
	{
		return new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(options);
				var client = services.AddHttpClient(options is PictureOptions ? "pictures" : WebhookClient.Name(UserRole.Member)).AddClientHttp<TOptions>();
				Optional.Of(wire).Apply(found => client.ConfigurePrimaryHttpMessageHandler(() => found));
			})
			.Build();
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

	private sealed class Throttling(bool retryAfter = true) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { RequestMessage = request };
			if (retryAfter)
			{
				response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
			}

			return Task.FromResult(response);
		}
	}

	private sealed class Failing : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request });
		}
	}

	private sealed class Counting : HttpMessageHandler
	{
		private int _answered;

		public int Answered => _answered;

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _answered);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request });
		}
	}
}
