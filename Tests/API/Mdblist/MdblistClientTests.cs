using System.Net;
using API.Mdblist;
using Bogus;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistClientTests
{
	private const string BaseUrl = "https://api.mdblist.com";

	[Test]
	public async Task SendAsync_AppendsApiKeyQueryParameter()
	{
		var apiKey = new Faker().Random.AlphaNumeric(32);
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var testee = new MdblistClient(handler.CreateClient(BaseUrl), new MdblistOptions { BaseUrl = BaseUrl }, IApiKeyConfigRepository.Mock().Object);

		await testee.SendAsync(HttpMethod.Get, "lists/1/items", new TestApiKey(ApiKeyProvider.Mdblist, apiKey));

		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches($".*apikey={apiKey}.*"), Times.Once);
	}

	[Test]
	public async Task SendAsync_WhenUnauthorized_InvalidatesCachedApiKey()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		var apiKeys = IApiKeyConfigRepository.Mock();
		var testee = new MdblistClient(handler.CreateClient(BaseUrl), new MdblistOptions { BaseUrl = BaseUrl }, apiKeys.Object);

		await testee.SendAsync(HttpMethod.Get, "lists/1/items", new TestApiKey(ApiKeyProvider.Mdblist, new Faker().Random.AlphaNumeric(32)));

		apiKeys.Invalidate(ApiKeyProvider.Mdblist).WasCalled(Times.Once);
	}
}
