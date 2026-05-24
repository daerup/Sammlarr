using System.Net;
using API.Tmdb;
using Bogus;
using Contracts.Auth;
using Contracts.Media;
using Shared.Options;

namespace API.Tests.TMDB;

public class TmdbClientTests
{
	private static TmdbOptions CreateOptions() => new() { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p/original" };

	[Test]
	public async Task SendAsync_WhenApiKeyMissing_ThrowsInvalidOperationException()
	{
		var options = CreateOptions();
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var apiKeyStore = IApiKeyConfigRepository.Mock();
		var testee = new TmdbClient(httpClient, options, apiKeyStore);

		await Assert.That(async () => await testee.SendAsync(HttpMethod.Get, "configuration")).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task SendAsync_WhenApiKeyExists_AppendsApiKeyQueryParameter()
	{
		var options = CreateOptions();
		var apiKey = GetApiKey();
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var apiKeyStore = IApiKeyConfigRepository.Mock();
		apiKeyStore.GetApiKeyAsync(MediaProvider.Tmdb, Any()).Returns(apiKey);
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var testee = new TmdbClient(httpClient, options, apiKeyStore);

		await testee.SendAsync(HttpMethod.Get, "trending/all/week?page=2");
		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches($".*api_key={apiKey}.*"), Times.Once);
	}

	[Test]
	public async Task ValidateApiKeyAsync_WhenRequestSucceeds_ReturnsTrue()
	{
		var options = CreateOptions();
		var apiKey = GetApiKey();
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.Accepted);
		var apiKeyStore = IApiKeyConfigRepository.Mock();
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var testee = new TmdbClient(httpClient, options, apiKeyStore);

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
		var apiKeyStore = IApiKeyConfigRepository.Mock();
		using var httpClient = handler.CreateClient(options.BaseUrl);
		var testee = new TmdbClient(httpClient, options, apiKeyStore);

		var result = await testee.ValidateApiKeyAsync(apiKey);

		await Assert.That(result).IsFalse();
	}

	private string GetApiKey() => new Faker().Random.AlphaNumeric(32);
}
