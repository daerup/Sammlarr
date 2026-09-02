using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts;

namespace DB.Data.Notifications;

[Table("NotificationDeliveryOutcomes")]
internal record NotificationDeliveryOutcomeRow : Entity<Guid>, IUserOwned
{
	public required Guid EventId { get; set; }

	public required Guid TargetId { get; set; }

	[Required]
	[MaxLength(100)]
	public required string EventType { get; set; }

	public required OutcomeKind Kind { get; set; }

	[MaxLength(1024)]
	[Sensitive]
	public string? Reason { get; set; }

	public DateTimeOffset? NextAttemptAt { get; set; }
}
