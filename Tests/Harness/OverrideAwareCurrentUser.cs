using Contracts.Identity;
using Domain.Identity;
using TestInfrastructure;

namespace Harness;

internal sealed class OverrideAwareCurrentUser(CurrentUserOverride overrides, TestCurrentUser fallback) : ICurrentUser
{
	private ICurrentUser Active => overrides.Value ?? fallback;

	public bool IsAuthenticated => Active.IsAuthenticated;

	public UserId Id => Active.Id;

	public string DisplayName => Active.DisplayName;

	public string AvatarUrl => Active.AvatarUrl;

	public UserRole Role => Active.Role;
}
