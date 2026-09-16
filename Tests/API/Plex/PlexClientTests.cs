using System.Net;
using API.Plex;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Providers;
using Polly.CircuitBreaker;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexClientTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	private static readonly Faker<TestCredential> credentialFaker = new Faker<TestCredential>().CustomInstantiator(f => new TestCredential(
		AuthProvider.Plex,
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
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(Error.From("No credential is stored for this provider."));
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

		await Assert
			.That(async () => await testee.SendAsync(HttpMethod.Get, PlexBaseUrl))
			.Throws<InvalidOperationException>()
			.WithMessageContaining("No valid Plex credentials are stored");
	}

	[Test]
	public async Task SendAsync_WhenTheResiliencePipelineGivesUp_ThrowsProviderUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new BrokenCircuitException("plex circuit is open"));
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credentialStore = ICredentialRepository.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credentialFaker.Generate());
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

		await Assert.That(async () => await testee.SendAsync(HttpMethod.Get, PlexBaseUrl)).Throws<ProviderUnavailableException>();
	}

	[Test]
	public async Task SendAsync_WhenCredentialExists_AddsAuthHeadersAndCustomHeaders()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = credentialFaker.Generate();
		var credentialStore = ICredentialRepository.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

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
		credentialStore.GetAsync(AuthProvider.Plex, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task SendAsync_ReadsCredentialForEachRequest()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = credentialFaker.Generate();
		var credentialStore = ICredentialRepository.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

		await testee.SendAsync(HttpMethod.Get, $"{PlexBaseUrl}/one");
		await testee.SendAsync(HttpMethod.Get, $"{PlexBaseUrl}/two");

		credentialStore.GetAsync(AuthProvider.Plex, Any()).WasCalled(Times.Exactly(2));
		await Assert.That(handler.Requests.Count).IsEqualTo(2);
	}

	[Test]
	public async Task SendAsync_WhenResponseIsUnauthorized_InvalidatesCachedCredential()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credentialStore = ICredentialRepository.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credentialFaker.Generate());
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

		await testee.SendAsync(HttpMethod.Get, PlexBaseUrl);

		credentialStore.Invalidate(AuthProvider.Plex).WasCalled(Times.Once);
	}
}
