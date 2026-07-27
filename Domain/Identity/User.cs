namespace Domain.Identity;

public sealed record User(UserId Id, string DisplayName, string AvatarUrl, UserRole Role);
