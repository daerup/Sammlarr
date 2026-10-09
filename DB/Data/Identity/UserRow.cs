using System.ComponentModel.DataAnnotations;
using Domain.Identity;
using Domain.Providers;

namespace DB.Data.Identity;

internal sealed record UserRow : Entity<Guid>
{
	public required AuthProvider Provider { get; set; }
	public required string ExternalUserId { get; set; }
	public required string DisplayName { get; set; }
	public required string TagName { get; set; }
	public required string AvatarUrl { get; set; }
	public required UserRole Role { get; set; }
	public required DateTimeOffset JoinedAt { get; set; }
	public double? Hue { get; set; }

	[MaxLength(2048)]
	public string? PictureUrl { get; set; }
	public double? PictureHue { get; set; }
	public double? PictureChroma { get; set; }

	[MaxLength(512)]
	public string? PasswordHash { get; set; }
	public int FailedSignIns { get; set; }
	public DateTimeOffset? LockedUntil { get; set; }
	public DateTimeOffset? SessionsRevokedAt { get; set; }

	[MaxLength(512)]
	public string? ConfigResetHash { get; set; }
}
