using Contracts.Identity;
using DB.Data.Identity;
using Domain.Identity;
using Harness;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Auth;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class AccountClaimsTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IUserClaimsPrincipalFactory<UserRow> Testee => host.Resolve<IUserClaimsPrincipalFactory<UserRow>>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task CreateAsync_ForAUser_CarriesTheirNameAvatarRoleAndSecurityStamp()
	{
		var id = await host.SeedExistingUser("plex-ada", UserRole.Admin, "Ada");

		var principal = await Testee.CreateAsync(await host.Resolve<UserManager<UserRow>>().RequireAsync(id));

		await Assert.That(principal.FindFirst(AuthClaims.DisplayName)?.Value).IsEqualTo("Ada");
		await Assert.That(principal.FindFirst(AuthClaims.Avatar)?.Value).IsEqualTo("avatar");
		await Assert.That(principal.IsInRole(nameof(UserRole.Admin))).IsTrue();
		await Assert.That(principal.FindFirst(new ClaimsIdentityOptions().SecurityStampClaimType)?.Value).IsEqualTo(await host.SecurityStampOf(id));
	}
}
