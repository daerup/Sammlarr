using Domain.Identity;
using Domain.Providers;

namespace DB.Data.Identity;

internal sealed record UserRow : Entity<Guid>
{
	public required AuthProvider Provider { get; set; }
	public required string ExternalUserId { get; set; }
	public required string DisplayName { get; set; }
	public required string AvatarUrl { get; set; }
	public required UserRole Role { get; set; }
	public required DateTimeOffset JoinedAt { get; set; }
}
