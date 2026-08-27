using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DB.Data.Notifications;

[Table("NotificationSubscriptions")]
internal record NotificationSubscriptionRow : Entity<Guid>, IUserOwned
{
	public required Guid TargetId { get; set; }

	[Required]
	[MaxLength(100)]
	public required string EventType { get; set; }
}
