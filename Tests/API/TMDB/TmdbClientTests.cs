using System.Net;
using API.Tmdb;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Providers;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.TMDB;

public class TmdbClientTests
{
	private static TmdbOptions CreateOptions() => new() { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p" };

	[Test]
	public async Task SendAsync_AppendsApiKeyQueryParameter()
	{
		var options = CreateOptions();
		var apiKey = GetApiKey();
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var testee = new TmdbClient(httpClient, options, IApiKeyService.Mock().Object, ICurrentUser.Mock().Object);

		await testee.SendAsync(HttpMethod.Get, "trending/all/week?page=2", new TestApiKey(ApiKeyProvider.Tmdb, apiKey));

		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches($".*api_key={apiKey}.*"), Times.Once);
	}

	[Test]
	public async Task SendAsync_WhenUnauthorized_InvalidatesCachedApiKey()
	{
		var options = CreateOptions();
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var apiKeys = IApiKeyService.Mock();
		var testee = new TmdbClient(httpClient, options, apiKeys.Object, ICurrentUser.Mock().Object);

		await testee.SendAsync(HttpMethod.Get, "trending/all/week?page=1", new TestApiKey(ApiKeyProvider.Tmdb, GetApiKey()));

		apiKeys.ReportRejected(ApiKeyProvider.Tmdb).WasCalled(Times.Once);
	}

	private string GetApiKey() => new Faker().Random.AlphaNumeric(32);
}
