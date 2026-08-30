using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DB.Conversion.Encryption;
using Domain.Notifications;

namespace DB.Data.Notifications;

[Table("NotificationTargets")]
internal record NotificationTargetRow : Entity<Guid>, IUserOwned
{
	[Required]
	[MaxLength(200)]
	public required string Name { get; set; }

	[Required]
	public required NotificationChannel Channel { get; set; }

	public bool Enabled { get; set; } = true;

	[MaxLength(2048)]
	public string? Url { get; set; }

	[MaxLength(4096)]
	[Encrypted]
	public string? SigningSecret { get; set; }

	[MaxLength(4096)]
	[Encrypted]
	public string? AppToken { get; set; }

	[MaxLength(4096)]
	[Encrypted]
	public string? UserKey { get; set; }

	[MaxLength(8192)]
	public string? BodyTemplate { get; set; }

	[MaxLength(2048)]
	public string? TitleTemplate { get; set; }

	[MaxLength(8192)]
	public string? MessageTemplate { get; set; }
}
