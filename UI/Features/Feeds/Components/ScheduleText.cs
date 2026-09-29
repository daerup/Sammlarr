using CronExpressionDescriptor;
using Domain.Feed;

namespace UI.Features.Feeds.Components;

internal static class ScheduleText
{
	internal static string Describe(FeedRunTrigger schedule) =>
		schedule is Scheduled scheduled ? ExpressionDescriptor.GetDescription(scheduled.ToString()) : "Manual";

	internal static string Ago(DateTimeOffset moment, DateTimeOffset now)
	{
		var elapsed = now - moment;

		return elapsed switch
		{
			{ TotalMinutes: < 60 } => $"{elapsed.TotalMinutes:0} min ago",
			{ TotalHours: < 24 } => $"{elapsed.TotalHours:0} hours ago",
			{ TotalDays: < 30 } => $"{elapsed.TotalDays:0} days ago",
			_ => moment.ToLocalTime().ToString("MMM d"),
		};
	}

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
