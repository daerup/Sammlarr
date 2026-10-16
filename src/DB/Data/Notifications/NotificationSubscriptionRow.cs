using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Notifications;

namespace DB.Data.Notifications;

[Table("NotificationSubscriptions")]
internal sealed record NotificationSubscriptionRow : Entity<Guid>, IUserOwned
{
	public required Guid TargetId { get; set; }

	[Required]
	public required List<NotificationEventType> EventTypes { get; set; }

	[MaxLength(8192)]
	public string? BodyTemplate { get; set; }

	[MaxLength(2048)]
	public string? TitleTemplate { get; set; }

	[MaxLength(8192)]
	public string? MessageTemplate { get; set; }

	public NotificationLevel? Level { get; set; }

	public PushoverPriority? Priority { get; set; }
}
