using System.Security.Claims;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Web.Auth;

namespace Web.Tests.Auth;

public class AuthEndpointsTests
{
	private readonly Mock<IAccountLinkService> _links = IAccountLinkService.Mock();

	[Test]
	public async Task JoinAsync_ForAnAllZeroUser_SendsBackToTheLinkPageWithoutRedeeming()
	{
		var result = await AuthEndpoints.JoinAsync(Guid.Empty, "token", "password", "password", new DefaultHttpContext(), _links.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo(LinkPage("join"));
		_links.JoinAsync(Any(), Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResetAsync_ForAnAllZeroUser_SendsBackToTheLinkPageWithoutRedeeming()
	{
		var result = await AuthEndpoints.ResetAsync(Guid.Empty, "token", "password", "password", new DefaultHttpContext(), _links.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo(LinkPage("reset"));
		_links.ResetPasswordAsync(Any(), Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	[Arguments("/settings/users", "/settings/users")]
	[Arguments("https://elsewhere.example/", "/")]
	public async Task SignOutAsync_EndsTheSessionAndReturnsOnlyToALocalPage(string returnUrl, string target)
	{
		var cookie = ISignInCookie.Mock();

		var result = await AuthEndpoints.SignOutAsync(returnUrl, cookie.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo(target);
		cookie.SignOutAsync().WasCalled(Times.Once);
	}

	[Test]
	public async Task CallbackAsync_InFirstSetup_WaitsForTheAdminToBeConfirmedAndKeepsTheSetupCode()
	{
		var pending = new PendingAdmin(Guid.NewGuid(), "Daerup", string.Empty, HomeRole.Admin);
		var signIn = ISignInService.Mock();
		signIn.PendAdminAsync(AuthProvider.Plex, Any(), Any()).Returns(Success.Of(pending));
		var context = WithCookies("sammlarr.oauth=state; sammlarr.setup=AHAMB6RZ");

		var result = await AuthEndpoints.CallbackAsync("plex", "state", "/setup", context, signIn.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo($"/setup?pending={pending.Id}");
		await Assert.That(SetCookies(context)).Contains(cookie => cookie.StartsWith($"sammlarr.pending-admin={pending.Id}", StringComparison.Ordinal));
		await Assert.That(SetCookies(context)).Contains(cookie => cookie.StartsWith("sammlarr.setup=AHAMB6RZ", StringComparison.Ordinal));
	}

	[Test]
	public async Task CallbackAsync_ForTheSignedInAdminSwapping_WaitsForTheSwapToBeConfirmed()
	{
		var pending = new PendingAdmin(Guid.NewGuid(), "Daerup", string.Empty, HomeRole.Admin);
		var signIn = ISignInService.Mock();
		signIn.PendAdminAsync(AuthProvider.Plex, Any(), Any()).Returns(Success.Of(pending));
		var context = WithCookies("sammlarr.oauth=state; sammlarr.admin-swap=1");
		context.User = SignedInAs(UserId.New());

		var result = await AuthEndpoints.CallbackAsync("plex", "state", "/setup", context, signIn.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo($"/setup?pending={pending.Id}");
		signIn.LinkAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task CallbackAsync_ForASignedInUserNotSwapping_LinksTheAccountAndReportsARefusalBack()
	{
		var user = UserId.New();
		var signIn = ISignInService.Mock();
		signIn.LinkAsync(AuthProvider.Plex, Any(), user, Any()).Returns(Error.From("Plex returned Mia instead of Daerup."));
		var context = WithCookies("sammlarr.oauth=state");
		context.User = SignedInAs(user);

		var result = await AuthEndpoints.CallbackAsync("plex", "state", "/setup", context, signIn.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo($"/setup?linkError={Uri.EscapeDataString("Plex returned Mia instead of Daerup.")}");
		signIn.PendAdminAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ConfirmAdminAsync_InFirstSetup_ConfirmsWithTheSetupCodeAndSignsTheNewAdminIn()
	{
		var pending = Guid.NewGuid();
		var signIn = ISignInService.Mock();
		var admin = Admin();
		signIn.ConfirmAdminAsync(pending, new Secret("AHAMB6RZ"), Any()).Returns(Success.Of(admin));
		var cookie = ISignInCookie.Mock();
		var context = WithCookies($"sammlarr.pending-admin={pending}; sammlarr.setup=AHAMB6RZ", cookie);

		var result = await AuthEndpoints.ConfirmAdminAsync(pending, context, signIn.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo("/setup");
		cookie.SignInAsync(admin.Id, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ConfirmAdminAsync_ForTheSignedInAdmin_SwapsTheAdminInsteadOfCreatingOne()
	{
		var pending = Guid.NewGuid();
		var signIn = ISignInService.Mock();
		var admin = Admin();
		signIn.SwapAdminAsync(pending, admin.Id, Any()).Returns(Success.Of(admin));
		var cookie = ISignInCookie.Mock();
		var context = WithCookies($"sammlarr.pending-admin={pending}", cookie);
		context.User = SignedInAs(admin.Id);

		var result = await AuthEndpoints.ConfirmAdminAsync(pending, context, signIn.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo("/setup");
		cookie.SignInAsync(admin.Id, Any()).WasCalled(Times.Once);
		signIn.ConfirmAdminAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ConfirmAdminAsync_FromATabShowingAnOlderSignIn_ConfirmsNothing()
	{
		var signIn = ISignInService.Mock();
		var context = WithCookies($"sammlarr.pending-admin={Guid.NewGuid()}; sammlarr.setup=AHAMB6RZ");

		var result = await AuthEndpoints.ConfirmAdminAsync(Guid.NewGuid(), context, signIn.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo(TimedOut);
		signIn.ConfirmAdminAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	[Arguments("")]
	[Arguments("sammlarr.setup=AHAMB6RZ")]
	public async Task ConfirmAdminAsync_WithoutAPendingAdmin_SaysItTimedOutAndConfirmsNothing(string cookies)
	{
		var signIn = ISignInService.Mock();

		var result = await AuthEndpoints.ConfirmAdminAsync(Guid.NewGuid(), WithCookies(cookies), signIn.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo(TimedOut);
		signIn.ConfirmAdminAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ConfirmAdminAsync_InFirstSetupWithoutTheSetupCode_SaysItTimedOut()
	{
		var pending = Guid.NewGuid();
		var signIn = ISignInService.Mock();

		var result = await AuthEndpoints.ConfirmAdminAsync(pending, WithCookies($"sammlarr.pending-admin={pending}"), signIn.Object);

		var redirect = await Assert.That(result).IsTypeOf<RedirectHttpResult>();
		await Assert.That(redirect?.Url).IsEqualTo(TimedOut);
		signIn.ConfirmAdminAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	private static string TimedOut => $"/setup?error={Uri.EscapeDataString("The Plex sign-in timed out. Please sign in again.")}&with=plex";

	private static User Admin() => new(UserId.New(), "Daerup", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch);

	private static ClaimsPrincipal SignedInAs(UserId user) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Value.ToString())], "test"));

	private static DefaultHttpContext WithCookies(string cookies, Mock<ISignInCookie>? signInCookie = null)
	{
		var context = new DefaultHttpContext();
		context.Request.Headers.Cookie = cookies;
		context.RequestServices = new ServiceCollection().AddSingleton((signInCookie ?? ISignInCookie.Mock()).Object).BuildServiceProvider();
		return context;
	}

	private static IEnumerable<string> SetCookies(HttpContext context) => context.Response.Headers.SetCookie.Select(cookie => cookie ?? string.Empty);

	private static string LinkPage(string page) => $"/{page}/{Guid.Empty}?token=token&error={Uri.EscapeDataString(UnusableLink.Message)}";
}
