using System.Net;
using API.Plex;
using Domain.Auth;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Plex;

public class PlexOAuthProviderTests
{
	private const string Callback = "https://sammlarr.test/auth/plex/callback?state=abc";

	[Test]
	public async Task BeginLoginAsync_WhenPlexHandsOutAPin_SendsThePersonToPlexsSignInWithThePinOnTheCallback()
	{
		var plex = Mock.HttpHandler();
		plex.OnAnyRequest().RespondWithJson("""{"id":4242,"code":"pin-code","authToken":null}""");

		var signIn = await Assert.That(await Testee(plex).BeginLoginAsync(Callback)).IsSuccess();

		await Assert.That(signIn).StartsWith("https://app.plex.tv/auth#?");
		await Assert.That(Uri.UnescapeDataString(signIn!)).Contains($"{Callback}&pinId=4242");
	}

	[Test]
	public async Task BeginLoginAsync_NamesTheProductOnPlexsSignInEncodedOnce()
	{
		var plex = Mock.HttpHandler();
		plex.OnAnyRequest().RespondWithJson("""{"id":4242,"code":"pin-code","authToken":null}""");

		var signIn = await Assert.That(await Testee(plex).BeginLoginAsync(Callback)).IsSuccess();

		await Assert.That(signIn).Contains("context%5Bdevice%5D%5Bproduct%5D=Sammlarr");
	}

	[Test]
	public async Task BeginLoginAsync_WhenPlexRefusesThePin_FailsSayingSo()
	{
		var plex = Mock.HttpHandler();
		plex.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var error = await Assert.That(await Testee(plex).BeginLoginAsync(Callback)).IsError();

		await Assert.That(error!.Reason).Contains("Plex could not start the sign-in");
	}

	[Test]
	public async Task CheckAsync_ForATokenPlexAccepts_IsValidAndSendsAToken()
	{
		var plex = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);

		await Assert.That(await Testee(plex).CheckAsync(Guid.NewGuid(), "plex-token")).IsEqualTo(AccessCheck.Valid);
		plex.Verify(request => request.HasHeader("X-Plex-Token"), Times.Once);
	}

	private static PlexOAuthProvider Testee(MockHttpHandler plex)
	{
		var options = new PlexOptions
		{
			BaseUrl = "https://plex.tv",
			Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
		};
		return new PlexOAuthProvider(new SingleClientHttpClientFactory(plex.CreateClient("https://plex.tv")), options, new PlexUrls(options));
	}
}
