using Microsoft.AspNetCore.Http;
using Web.Auth;

namespace Web.Tests.Auth;

public class OAuthStateTests
{
	[Test]
	public async Task Redeem_WithTheStateThisBrowserWasGiven_Succeeds()
	{
		var state = OAuthState.Issue(new DefaultHttpContext());

		await Assert.That(OAuthState.Redeem(Returning(state), state)).IsTrue();
	}

	[Test]
	public async Task Redeem_WithAStateFromAnotherBrowser_Fails()
	{
		var mine = OAuthState.Issue(new DefaultHttpContext());
		var theirs = OAuthState.Issue(new DefaultHttpContext());

		await Assert.That(OAuthState.Redeem(Returning(mine), theirs)).IsFalse();
	}

	[Test]
	public async Task Redeem_WithoutTheCookie_Fails()
	{
		var state = OAuthState.Issue(new DefaultHttpContext());

		await Assert.That(OAuthState.Redeem(new DefaultHttpContext(), state)).IsFalse();
	}

	private static DefaultHttpContext Returning(string state)
	{
		var context = new DefaultHttpContext();
		context.Request.Headers.Cookie = $"{OAuthState.CookieName}={state}";
		return context;
	}
}
