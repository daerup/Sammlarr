using System.Net;
using API.Tmdb;
using Bogus;
using Shared.Options;
using Test.Shared;

namespace API.Tests.TMDB;

public class TmdbClientTests
{
	private static TmdbOptions CreateOptions() => new() { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p/original" };

	[Test]
	public async Task SendAsync_AppendsApiKeyQueryParameter()
	{
		var options = CreateOptions();
		var apiKey = GetApiKey();
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var testee = new TmdbClient(httpClient, options);

		await testee.SendAsync(HttpMethod.Get, "trending/all/week?page=2", new TestApiKey(ApiKeyProvider.Tmdb, apiKey));

		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches($".*api_key={apiKey}.*"), Times.Once);
	}

	[Test]
	public async Task ValidateApiKeyAsync_WhenRequestSucceeds_ReturnsTrue()
	{
		var options = CreateOptions();
		var apiKey = GetApiKey();
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.Accepted);
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var testee = new TmdbClient(httpClient, options);

		var result = await testee.ValidateApiKeyAsync(apiKey);

		await Assert.That(result).IsTrue();
		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches($".*api_key={apiKey}.*"), Times.Once);
		handler.VerifyNoUnmatchedRequests();
	}

	[Test]
	public async Task ValidateApiKeyAsync_WhenHandlerThrows_ReturnsFalse()
	{
		var options = CreateOptions();
		var apiKey = GetApiKey();
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new HttpRequestException("network"));
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var testee = new TmdbClient(httpClient, options);

		var result = await testee.ValidateApiKeyAsync(apiKey);

		await Assert.That(result).IsFalse();
	}

	private string GetApiKey() => new Faker().Random.AlphaNumeric(32);
}
