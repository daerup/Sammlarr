using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts;
using Domain.Notifications;

namespace DB.Data.Notifications;

[Table("NotificationDeliveryOutcomes")]
internal sealed record NotificationDeliveryOutcomeRow : Entity<Guid>, IUserOwned
{
	public required Guid EventId { get; set; }

	public required Guid TargetId { get; set; }

	public required NotificationEventType EventType { get; set; }

	public required OutcomeKind Kind { get; set; }

	[MaxLength(1024)]
	[Sensitive]
	public string? Reason { get; set; }

	public DateTimeOffset? NextAttemptAt { get; set; }
}
