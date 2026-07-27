using System.Net;
using API.Mdblist;
using Bogus;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistApiKeyTests
{
	private const string BaseUrl = "https://api.mdblist.com";

	[Test]
	public async Task CreateAsync_WhenKeyValid_ReturnsKeyAndSendsItAsQueryParameter()
	{
		var apiKey = new Faker().Random.AlphaNumeric(32);
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var factory = new SingleClientHttpClientFactory(handler.CreateClient(BaseUrl));

		var result = await MdblistApiKey.CreateAsync(apiKey, factory, new MdblistOptions { BaseUrl = BaseUrl });

		await Assert.That(result).IsSuccess().And.Multiple(async key => await Assert.That(key.Value).IsEqualTo(apiKey));
		handler.Verify(request => request.PathMatches($".*apikey={apiKey}.*"), Times.Once);
	}

	[Test]
	public async Task CreateAsync_WhenKeyRejected_ReturnsError()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		var factory = new SingleClientHttpClientFactory(handler.CreateClient(BaseUrl));

		var result = await MdblistApiKey.CreateAsync(new Faker().Random.AlphaNumeric(32), factory, new MdblistOptions { BaseUrl = BaseUrl });

		await Assert.That(result).IsError();
	}

	[Test]
	public async Task CreateAsync_WhenProviderUnreachable_ReturnsError()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new HttpRequestException("network"));
		var factory = new SingleClientHttpClientFactory(handler.CreateClient(BaseUrl));

		var result = await MdblistApiKey.CreateAsync(new Faker().Random.AlphaNumeric(32), factory, new MdblistOptions { BaseUrl = BaseUrl });

		await Assert.That(result).IsError();
	}
}
