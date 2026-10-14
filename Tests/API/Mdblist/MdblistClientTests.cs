using System.Net;
using API.Mdblist;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Providers;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using TestInfrastructure;

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
		handler.OnAnyRequest().Respond().WithHeader("x-ratelimit-remaining", "962").WithHeader("x-ratelimit-reset", MdblistApis.Reset);
		using var host = MdblistApis.Module(handler);

		using var response = await host.Services.GetRequiredService<MdblistClient>().SendAsync(HttpMethod.Get, "lists/1/items", Key);

		await Assert
			.That(host.Services.GetRequiredService<MdblistBudget>().Today(Key.Value).Answered.Map(answered => answered.Remaining))
			.IsSome()
			.And.IsEqualTo(962);
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
			.WithHeader("x-ratelimit-reset", MdblistApis.Reset);
		using var host = MdblistApis.Module(handler);
		var testee = host.Services.GetRequiredService<MdblistClient>();

		using var response = await testee.SendAsync(HttpMethod.Get, "lists/1/items", Key);

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		await Assert.That(host.Services.GetRequiredService<MdblistBudget>().BlockedUntil(Key.Value, LaneClass.Title, MdblistApis.Clock().Now)).IsSome();
	}

	[Test]
	public async Task SendAsync_ForATitleLookupAtTheFeedReserve_RefusesBeforeSending()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond();
		using var host = MdblistApis.Module(handler);
		host.Services.GetRequiredService<MdblistBudget>().Record(Key.Value, Spent());
		using var title = LanePriority.Next(LaneClass.Title).Enter();

		var refused = await Assert
			.That(async () => await host.Services.GetRequiredService<MdblistClient>().SendAsync(HttpMethod.Post, "imdb/movie", Key))
			.Throws<RemoteUnavailableException>();

		await Assert.That(refused!.Sent).IsFalse();
		handler.Verify(request => request.PathMatches(".*"), Times.Never);
	}

	[Test]
	public async Task SendAsync_ForAListPageAtTheFeedReserve_StillSends()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"movies":[],"shows":[]}""");
		using var host = MdblistApis.Module(handler);
		host.Services.GetRequiredService<MdblistBudget>().Record(Key.Value, Spent());
		using var page = LanePriority.Next(LaneClass.Source).Enter();

		using var response = await host.Services.GetRequiredService<MdblistClient>().SendAsync(HttpMethod.Get, "lists/1/items", Key);

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
	}

	private static HttpResponseMessage Spent() => MdblistApis.RateLimited(10, DateTimeOffset.UtcNow.AddDays(1));

	[Test]
	public async Task SendAsync_WhenUnauthorized_InvalidatesCachedApiKey()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		var apiKeys = IApiKeyService.Mock();
		var testee = new MdblistClient(handler.CreateClient(BaseUrl), new MdblistOptions { BaseUrl = BaseUrl }, apiKeys.Object, ICurrentUser.Mock().Object);

		await testee.SendAsync(HttpMethod.Get, "lists/1/items", new TestApiKey(ApiKeyProvider.Mdblist, new Faker().Random.AlphaNumeric(32)));

		apiKeys.ReportRejected(ApiKeyProvider.Mdblist).WasCalled(Times.Once);
	}
}
