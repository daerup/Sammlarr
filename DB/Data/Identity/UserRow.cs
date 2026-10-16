using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace DB.Data.Identity;

internal sealed class UserRow : IdentityUser<Guid>, IAuditable
{
	public required string DisplayName { get; set; }
	public required string TagName { get; set; }
	public required string AvatarUrl { get; set; }
	public DateTimeOffset? JoinedAt { get; set; }
	public double? Hue { get; set; }

	[MaxLength(2048)]
	public string? PictureUrl { get; set; }
	public double? PictureHue { get; set; }
	public double? PictureChroma { get; set; }

	[MaxLength(512)]
	public string? ConfigResetHash { get; set; }

	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
