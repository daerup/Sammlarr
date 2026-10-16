using System.ComponentModel;

namespace Domain.Notifications;

public enum PushoverPriority
{
	[Description("Lowest (-2)")]
	Lowest = -2,

	[Description("Low (-1)")]
	Low = -1,

	[Description("Normal (0)")]
	Normal = 0,

	[Description("High (1)")]
	High = 1,

	[Description("Emergency (2)")]
	Emergency = 2,
}

public static class PushoverPriorities
{
	public static IReadOnlyList<PushoverPriority> Ascending { get; } = [.. Enum.GetValues<PushoverPriority>().OrderBy(priority => (int)priority)];

	extension(NotificationLevel level)
	{
		public PushoverPriority AsPushoverPriority() =>
			level switch
			{
				NotificationLevel.Info => PushoverPriority.Normal,
				NotificationLevel.Success => PushoverPriority.Normal,
				NotificationLevel.Warning => PushoverPriority.Normal,
				NotificationLevel.Error => PushoverPriority.High,
			};
	}
}
