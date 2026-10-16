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

	private static string LinkPage(string page) => $"/{page}/{Guid.Empty}/token?error={Uri.EscapeDataString(UnusableLink.Message)}";
}
