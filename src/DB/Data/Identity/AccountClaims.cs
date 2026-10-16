using System.Security.Claims;
using Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DB.Data.Identity;

internal sealed class AccountClaims(UserManager<UserRow> users, RoleManager<IdentityRole<Guid>> roles, IOptions<IdentityOptions> options)
	: UserClaimsPrincipalFactory<UserRow, IdentityRole<Guid>>(users, roles, options)
{
	protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UserRow user)
	{
		var identity = await base.GenerateClaimsAsync(user);
		identity.AddClaim(new Claim(AuthClaims.DisplayName, user.DisplayName));
		identity.AddClaim(new Claim(AuthClaims.Avatar, user.AvatarUrl));
		return identity;
	}
}
