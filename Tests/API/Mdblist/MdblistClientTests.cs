using System.Net;
using API.Http;
using API.Mdblist;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Providers;
using Contracts.Usage;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.DI;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Mdblist;

public class MdblistClientTests
{
	private const string BaseUrl = "https://api.mdblist.com";

	private static readonly TestApiKey Key = new(ApiKeyProvider.Mdblist, "mdblist-key");

	[Test]
	public async Task SendAsync_AppendsApiKeyQueryParameter()
	{
		var apiKey = new Faker().Random.AlphaNumeric(32);
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var testee = new MdblistClient(
			handler.CreateClient(BaseUrl),
			new MdblistOptions { BaseUrl = BaseUrl },
			IApiKeyService.Mock().Object,
			ICurrentUser.Mock().Object
		);

		await testee.SendAsync(HttpMethod.Get, "lists/1/items", new TestApiKey(ApiKeyProvider.Mdblist, apiKey));

		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches($".*apikey={apiKey}.*"), Times.Once);
	}

	[Test]
	public async Task SendAsync_ForAnyRequest_RecordsTheRequestsMdblistHasLeft()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.OK).WithHeader("x-ratelimit-remaining", "962").WithHeader("x-ratelimit-reset", Reset);
		using var host = Host(handler);

		using var response = await host.Services.GetRequiredService<MdblistClient>().SendAsync(HttpMethod.Get, "lists/1/items", Key);

		await Assert.That(host.Services.GetRequiredService<MdblistBudget>().Left(Key.Value)).IsSome().And.IsEqualTo(962);
	}

	[Test]
	public async Task SendAsync_WhenASpentKeysRetryAfterOutlastsTheMaxWait_HandsBackThe429AndRecordsTheKeyAsSpent()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.Respond(HttpStatusCode.TooManyRequests)
			.WithHeader("Retry-After", "3600")
			.WithHeader("x-ratelimit-remaining", "0")
			.WithHeader("x-ratelimit-reset", Reset);
		using var host = Host(handler);
		var testee = host.Services.GetRequiredService<MdblistClient>();

		using var response = await testee.SendAsync(HttpMethod.Get, "lists/1/items", Key);

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		await Assert.That(host.Services.GetRequiredService<MdblistBudget>().ReservedUntil(Key.Value)).IsSome();
	}

	[Test]
	public async Task SendAsync_WhenUnauthorized_InvalidatesCachedApiKey()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		var apiKeys = IApiKeyService.Mock();
		var testee = new MdblistClient(handler.CreateClient(BaseUrl), new MdblistOptions { BaseUrl = BaseUrl }, apiKeys.Object, ICurrentUser.Mock().Object);

		await testee.SendAsync(HttpMethod.Get, "lists/1/items", new TestApiKey(ApiKeyProvider.Mdblist, new Faker().Random.AlphaNumeric(32)));

		apiKeys.ReportRejected(ApiKeyProvider.Mdblist).WasCalled(Times.Once);
	}

	private static string Reset => $"{DateTimeOffset.UnixEpoch.AddDays(2).ToUnixTimeSeconds()}";

	private static IHost Host(MockHttpHandler handler)
	{
		return new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddLogging();
				services.AddSingleton(
					new MdblistOptions
					{
						BaseUrl = BaseUrl,
						Http = new ProviderHttpOptions { AttemptTimeoutSeconds = 1 },
					}
				);
				services.AddModule<HttpModule>();
				services.AddSingleton(MdblistApis.Clock());
				services.AddSingleton(IProviderRateLimitReporter.Mock().Object);
				services.AddSingleton(IFeedRunUsage.Mock().Object);
				services.AddSingleton(IApiKeyService.Mock().Object);
				services.AddSingleton(ICurrentUser.Mock().Object);
				new MdblistModule().RegisterServices(services);
				services.AddHttpClient<MdblistClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
			})
			.Build();
	}
}
