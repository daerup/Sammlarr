namespace Contracts.Auth;

public record AuthorizedUser(string DisplayName, string AvatarUrl, string Email, string SubscriptionDescription, DateTimeOffset JoinedAt, string AccountUrl);
