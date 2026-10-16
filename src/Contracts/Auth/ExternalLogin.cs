namespace Contracts.Auth;

public record ExternalLogin(string ExternalUserId, string DisplayName, string AvatarUrl, OAuthTokenGrant Token);
