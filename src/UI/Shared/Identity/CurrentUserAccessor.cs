using System.Security.Claims;
using Contracts.Identity;
using Domain.Identity;

namespace UI.Shared.Identity;

internal sealed class CurrentUserAccessor(CurrentUserOverride currentUserOverride) : ICurrentUser
{
	private ClaimsUser claims = new(new ClaimsPrincipal(new ClaimsIdentity()));

	public void SetPrincipal(ClaimsPrincipal value) => claims = new ClaimsUser(value);

	private ICurrentUser Active => currentUserOverride.Value.GetValueOr(claims);

	public bool IsAuthenticated => Active.IsAuthenticated;

	public UserId Id => Active.Id;

	public string DisplayName => Active.DisplayName;

	public string AvatarUrl => Active.AvatarUrl;

	public UserRole Role => Active.Role;

	private sealed class ClaimsUser(ClaimsPrincipal principal) : ICurrentUser
	{
		public bool IsAuthenticated => principal.Identity?.IsAuthenticated == true && principal.FindFirst(ClaimTypes.NameIdentifier) is not null;

		public UserId Id
		{
			get
			{
				return Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
					? UserId.From(id)
					: throw new InvalidOperationException("No authenticated user is present.");
			}
		}

		public string DisplayName => principal.FindFirst(AuthClaims.DisplayName)?.Value ?? string.Empty;

		public string AvatarUrl => principal.FindFirst(AuthClaims.Avatar)?.Value ?? string.Empty;

		public UserRole Role => Enum.TryParse<UserRole>(principal.FindFirst(ClaimTypes.Role)?.Value, out var role) ? role : UserRole.Member;
	}
}
