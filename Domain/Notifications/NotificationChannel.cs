using System.ComponentModel;

namespace Domain.Notifications;

public enum NotificationChannel
{
	Webhook,
	Pushover,

	[Description("In-app toasts")]
	WebUi,
}
