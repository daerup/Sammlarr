namespace Domain.Identity;

public sealed record User(UserId Id, string DisplayName, string AvatarUrl, UserRole Role, DateTimeOffset JoinedAt)
{
	public string TagName { get; init; } = DisplayName;
}
