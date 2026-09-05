using System.Security.Claims;
using Contracts.Identity;
using Domain.Identity;

namespace UI.Shared.Identity;

internal sealed class CurrentUserAccessor(CurrentUserOverride currentUserOverride) : ICurrentUser
{
	private ClaimsPrincipal principal = new(new ClaimsIdentity());

	public void SetPrincipal(ClaimsPrincipal value) => principal = value;

	public bool IsAuthenticated =>
		currentUserOverride.Value?.IsAuthenticated ?? (principal.Identity?.IsAuthenticated == true && principal.FindFirst(ClaimTypes.NameIdentifier) is not null);

	public UserId Id =>
		currentUserOverride.Value?.Id
		?? (
			Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
				? UserId.From(id)
				: throw new InvalidOperationException("No authenticated user is present.")
		);

	public string DisplayName => currentUserOverride.Value?.DisplayName ?? principal.Identity?.Name ?? string.Empty;

	public string AvatarUrl => currentUserOverride.Value?.AvatarUrl ?? principal.FindFirst(AuthClaims.Avatar)?.Value ?? string.Empty;

	public UserRole Role =>
		currentUserOverride.Value?.Role ?? (Enum.TryParse<UserRole>(principal.FindFirst(ClaimTypes.Role)?.Value, out var role) ? role : UserRole.Member);
}
