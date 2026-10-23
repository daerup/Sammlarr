using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Notifications;

namespace DB.Data.Notifications;

[Table("NotificationTargets")]
internal sealed record NotificationTargetRow : Entity<Guid>, IUserOwned
{
	[Required]
	[MaxLength(200)]
	public required string Name { get; set; }

	[Required]
	public required NotificationChannel Channel { get; set; }

	public bool Enabled { get; set; } = true;

	[MaxLength(4096)]
	public Secret? Url { get; set; }

	[MaxLength(4096)]
	public Secret? SigningSecret { get; set; }

	[MaxLength(4096)]
	public Secret? AppToken { get; set; }

	[MaxLength(4096)]
	public Secret? UserKey { get; set; }
}
