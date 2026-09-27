using CronExpressionDescriptor;
using Domain.Feed;

namespace UI.Features.Feeds.Components;

internal static class ScheduleText
{
	internal static string Describe(FeedRunTrigger schedule) =>
		schedule is Scheduled scheduled ? ExpressionDescriptor.GetDescription(scheduled.ToString()) : "Manual";

	internal static string Away(DateTimeOffset at, DateTimeOffset from)
	{
		var span = at - from;
		if (span < TimeSpan.Zero)
		{
			return "due now";
		}
		if (span < TimeSpan.FromMinutes(1))
		{
			return "in under a minute";
		}
		if (span < TimeSpan.FromHours(1))
		{
			return $"in {span.Minutes} min";
		}
		if (span < TimeSpan.FromDays(1))
		{
			return span.Minutes == 0 ? $"in {span.Hours} hr" : $"in {span.Hours} hr {span.Minutes} min";
		}
		return span.Days == 1 ? "tomorrow" : $"in {span.Days} days";
	}
}
