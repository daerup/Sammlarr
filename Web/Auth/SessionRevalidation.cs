using System.Security.Claims;
using Contracts.Identity;
using Domain.Identity;

namespace Web.Auth;

internal static class SessionRevalidation
{
	public static async Task<bool> IsCurrentAsync(
		ClaimsPrincipal principal,
		DateTimeOffset issuedAt,
		ISessionValidator validator,
		CancellationToken cancellationToken
	)
	{
		if (
			Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var idValue) is false
			|| Enum.TryParse<UserRole>(principal.FindFirst(ClaimTypes.Role)?.Value, out var role) is false
		)
		{
			return false;
		}

		return await validator.IsValidAsync(UserId.From(idValue), role, issuedAt, cancellationToken);
	}
}
