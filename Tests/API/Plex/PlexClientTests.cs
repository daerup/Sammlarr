using System.Net;
using API.Plex;
using Bogus;
using Contracts.Auth;
using Test.Shared;

namespace API.Tests.Plex;

public class PlexClientTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	private static readonly Faker<TestCredential> credentialFaker = new Faker<TestCredential>().CustomInstantiator(f => new TestCredential(
		WatchlistProvider.Plex,
		f.Random.Guid(),
		f.Random.AlphaNumeric(32),
		f.Date.FutureOffset()
	));

	[Test]
	public async Task SendAsync_WhenCredentialMissing_ThrowsInvalidOperationException()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credentialStore = ICredentialRepository.Mock();
		var testee = new PlexClient(httpClient, credentialStore);

		await Assert
			.That(async () => await testee.SendAsync(HttpMethod.Get, PlexBaseUrl))
			.Throws<InvalidOperationException>()
			.WithMessageContaining("No valid Plex credentials are stored");
	}

	[Test]
	public async Task SendAsync_WhenCredentialExists_AddsAuthHeadersAndCustomHeaders()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = credentialFaker.Generate();
		var credentialStore = ICredentialRepository.Mock();
		credentialStore.GetAsync(WatchlistProvider.Plex, Any()).Returns(credential);
		var testee = new PlexClient(httpClient, credentialStore);

		await testee.SendAsync(HttpMethod.Get, PlexBaseUrl, customHeaders: [("X-Custom", "abc")]);

		handler.Verify(
			request =>
				request
					.Method(HttpMethod.Get)
					.Header("X-Plex-Token", credential.AccessToken)
					.Header("X-Plex-Client-Identifier", credential.ClientId.ToString())
					.Header("X-Custom", "abc"),
			Times.Once
		);
		credentialStore.GetAsync(WatchlistProvider.Plex, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task SendAsync_WhenCalledTwice_DoesNotReReadCredentialAfterHeadersExist()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = credentialFaker.Generate();
		var credentialStore = ICredentialRepository.Mock();
		credentialStore.GetAsync(WatchlistProvider.Plex, Any()).Returns(credential);
		var testee = new PlexClient(httpClient, credentialStore);

		await testee.SendAsync(HttpMethod.Get, $"{PlexBaseUrl}/one");
		await testee.SendAsync(HttpMethod.Get, $"{PlexBaseUrl}/two");

		credentialStore.GetAsync(WatchlistProvider.Plex, Any()).WasCalled(Times.Once);
		await Assert.That(handler.Requests.Count).IsEqualTo(2);
	}
}
