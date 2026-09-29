using System.Net;
using API.Tmdb;
using Bogus;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.TMDB;

public class TmdbApiKeyTests
{
	private static TmdbOptions CreateOptions() => new() { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p" };

	[Test]
	public async Task CreateAsync_WhenKeyValid_ReturnsKeyAndSendsItAsQueryParameter()
	{
		var options = CreateOptions();
		var apiKey = new Faker().Random.AlphaNumeric(32);
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var factory = new SingleClientHttpClientFactory(handler.CreateClient(options.BaseUrl));

		var result = await TmdbApiKey.CreateAsync(apiKey, factory, options);

		await Assert.That(result).IsSuccess().And.Multiple(async key => await Assert.That(key.Value).IsEqualTo(apiKey));
		handler.Verify(request => request.PathMatches($".*api_key={apiKey}.*"), Times.Once);
	}

	[Test]
	public async Task CreateAsync_WhenKeyRejected_ReturnsError()
	{
		var options = CreateOptions();
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		var factory = new SingleClientHttpClientFactory(handler.CreateClient(options.BaseUrl));

		var result = await TmdbApiKey.CreateAsync(new Faker().Random.AlphaNumeric(32), factory, options);

		await Assert.That(result).IsError();
	}

	[Test]
	public async Task CreateAsync_WhenProviderUnreachable_ReturnsError()
	{
		var options = CreateOptions();
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new HttpRequestException("network"));
		var factory = new SingleClientHttpClientFactory(handler.CreateClient(options.BaseUrl));

		var result = await TmdbApiKey.CreateAsync(new Faker().Random.AlphaNumeric(32), factory, options);

		await Assert.That(result).IsError();
	}
}
