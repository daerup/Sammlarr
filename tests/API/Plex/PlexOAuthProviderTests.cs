using System.Net;
using API.Plex;
using Contracts.Auth;
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
	public async Task CheckAsync_ForATokenPlexAccepts_IsValidAndSendsTheToken()
	{
		var plex = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);

		await Assert.That(await Testee(plex).CheckAsync(Guid.NewGuid(), new Secret("plex-token"))).IsEqualTo(AccessCheck.Valid);
		plex.Verify(request => request.Header("X-Plex-Token", "plex-token"), Times.Once);
	}

	[Test]
	[Arguments("""{"uuid":"mia","username":"","title":"Mia","home":true,"homeAdmin":true,"restricted":false}""", HomeRole.Admin)]
	[Arguments("""{"uuid":"mia","username":"","title":"Mia","home":true,"homeAdmin":false,"restricted":false}""", HomeRole.Member)]
	[Arguments("""{"uuid":"mia","username":"","title":"Mia","home":true,"homeAdmin":false,"restricted":true}""", HomeRole.Managed)]
	[Arguments("""{"uuid":"mia","username":"","title":"Mia","home":false,"homeAdmin":false,"restricted":false}""", HomeRole.None)]
	[Arguments("""{"uuid":"mia","username":"","title":"Mia"}""", HomeRole.None)]
	public async Task CompleteLoginAsync_ReadsTheProfilesPlaceInThePlexHome(string user, HomeRole home)
	{
		var plex = Mock.HttpHandler();
		plex.OnGet("/api/v2/pins/4242").RespondWithJson("""{"id":4242,"code":"pin-code","authToken":"plex-token","expiresAt":"2026-10-25T12:00:00Z"}""");
		plex.OnGet("/api/v2/user").RespondWithJson(user);

		var login = await Assert.That(await Testee(plex).CompleteLoginAsync($"pinId=4242&clientId={Guid.NewGuid()}")).IsSuccess();

		await Assert.That(login!.Home).IsEqualTo(home);
	}

	[Test]
	[Arguments(HomeRole.None)]
	[Arguments(HomeRole.Admin)]
	[Arguments(HomeRole.Member)]
	public async Task AsAdmin_ForAnAccountOfItsOwn_LetsItBeTheAdmin(HomeRole home)
	{
		var login = Login(home);

		await Assert.That(Testee(Mock.HttpHandler()).AsAdmin(login)).IsSuccess().And.IsEqualTo(login);
	}

	[Test]
	public async Task AsAdmin_ForAManagedHomeProfile_RefusesSayingWhy()
	{
		var refusal = await Assert.That(Testee(Mock.HttpHandler()).AsAdmin(Login(HomeRole.Managed))).IsError();

		await Assert.That(refusal!.Reason).IsEqualTo("A managed Home profile can't be the admin. Sign in with your own Plex profile instead.");
	}

	private static ExternalLogin Login(HomeRole home)
	{
		return new ExternalLogin("mia", "Mia", string.Empty, new OAuthTokenGrant(new Secret("plex-token"), DateTimeOffset.UnixEpoch, Guid.Empty)) { Home = home };
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
