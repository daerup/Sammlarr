using System.Security.Claims;
using Contracts.Identity;
using Core.Identity;
using Domain.Identity;
using UI.Features.Auth;

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
			Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
				? UserId.From(id)
				: throw new InvalidOperationException("No authenticated user is present.")
		);

	public string DisplayName => currentUserOverride.Value?.DisplayName ?? principal.Identity?.Name ?? string.Empty;

	public string AvatarUrl => currentUserOverride.Value?.AvatarUrl ?? principal.FindFirstValue(AuthEndpoints.AvatarClaim) ?? string.Empty;

	public UserRole Role =>
		currentUserOverride.Value?.Role ?? (Enum.TryParse<UserRole>(principal.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.Member);
}
