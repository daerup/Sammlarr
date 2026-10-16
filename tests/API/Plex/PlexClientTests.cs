using System.Net;
using API.Http.Lanes;
using API.Plex;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;
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
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(Error.From("No credential is stored for this provider."));
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

		await Assert
			.That(async () => await testee.SendAsync(HttpMethod.Get, PlexBaseUrl))
			.Throws<InvalidOperationException>()
			.WithMessageContaining("No valid Plex credentials are stored");
	}

	[Test]
	public async Task SendAsync_WhenTheCredentialWasRejected_ThrowsItAsRejected()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credentialStore = ICredentialService.Mock();
		var rejected = new SecretRejectedException("The stored Plex secret was rejected.");
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(Error.From(rejected.Message, rejected));
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

		await Assert.That(async () => await testee.SendAsync(HttpMethod.Get, PlexBaseUrl)).Throws<SecretRejectedException>();
	}

	[Test]
	public async Task SendAsync_WhenTheLaneRefuses_ThrowsProviderUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new LaneRefusedException("down"));
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credentialFaker.Generate());
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

		await Assert.That(async () => await testee.SendAsync(HttpMethod.Get, PlexBaseUrl)).Throws<RemoteUnavailableException>();
	}

	[Test]
	public async Task SendAsync_WhenCredentialExists_AddsAuthHeadersAndCustomHeaders()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = credentialFaker.Generate();
		var credentialStore = ICredentialService.Mock();
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
	}

	[Test]
	public async Task SendAsync_AfterANewCredentialIsStored_SendsItsToken()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credentialFaker.Generate());
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);
		await testee.SendAsync(HttpMethod.Get, $"{PlexBaseUrl}/one");
		var renewed = credentialFaker.Generate();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(renewed);

		await testee.SendAsync(HttpMethod.Get, $"{PlexBaseUrl}/two");

		handler.Verify(request => request.Path("/two").Header("X-Plex-Token", renewed.AccessToken), Times.Once);
	}

	[Test]
	public async Task SendAsync_WhenResponseIsUnauthorized_InvalidatesCachedCredential()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		using var httpClient = handler.CreateClient(PlexBaseUrl);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credentialFaker.Generate());
		var testee = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);

		await testee.SendAsync(HttpMethod.Get, PlexBaseUrl);

		credentialStore.ReportRejected(AuthProvider.Plex).WasCalled(Times.Once);
	}
}
