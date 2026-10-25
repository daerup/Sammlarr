using Domain.Auth;

namespace Contracts.Auth;

public record ExternalLogin(string ExternalUserId, string DisplayName, string AvatarUrl, OAuthTokenGrant Token)
{
	public HomeRole Home { get; init; } = HomeRole.None;
}
