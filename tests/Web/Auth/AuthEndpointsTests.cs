using Contracts.Auth;
using Contracts.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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

	private static string LinkPage(string page) => $"/{page}/{Guid.Empty}?token=token&error={Uri.EscapeDataString(UnusableLink.Message)}";
}
