using System.Security.Claims;
using Contracts.Identity;
using Core.Identity;
using Domain.Identity;
using UI.Features.Auth;

namespace UI.Shared.Identity;

internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor, CurrentUserOverride currentUserOverride) : ICurrentUser
{
	private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

	public bool IsAuthenticated =>
		currentUserOverride.Value?.IsAuthenticated ?? (Principal?.Identity?.IsAuthenticated == true && Principal.FindFirst(ClaimTypes.NameIdentifier) is not null);

	public UserId Id =>
		currentUserOverride.Value?.Id
		?? (
			Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
				? UserId.From(id)
				: throw new InvalidOperationException("No authenticated user is present.")
		);

	public string DisplayName => currentUserOverride.Value?.DisplayName ?? Principal?.Identity?.Name ?? string.Empty;

	public string AvatarUrl => currentUserOverride.Value?.AvatarUrl ?? Principal?.FindFirstValue(AuthEndpoints.AvatarClaim) ?? string.Empty;

	public UserRole Role =>
		currentUserOverride.Value?.Role ?? (Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.Member);
}
