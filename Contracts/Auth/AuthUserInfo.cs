namespace Contracts.Auth;

public record AuthUserInfo(string DisplayName, string AvatarUrl, string Email, string SubscriptionDescription, DateTimeOffset JoinedAt, string AccountUrl);
