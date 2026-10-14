using System.Globalization;
using CronExpressionDescriptor;
using Domain.Feed;

namespace UI.Features.Feeds.Components;

internal static class ScheduleText
{
	private static readonly Options English = new() { Locale = "en" };

	internal static string Describe(FeedRunTrigger schedule)
	{
		return schedule is Scheduled scheduled ? ExpressionDescriptor.GetDescription(scheduled.ToString(), English) : "Manual";
	}

	internal static string Ago(DateTimeOffset moment, DateTimeOffset now)
	{
		var elapsed = now - moment;

		return elapsed switch
		{
			{ TotalMinutes: < 60 } => FormattableString.CurrentCulture($"{elapsed.TotalMinutes:0} min ago"),
			{ TotalHours: < 24 } => FormattableString.CurrentCulture($"{elapsed.TotalHours:0} hours ago"),
			{ TotalDays: < 30 } => FormattableString.CurrentCulture($"{elapsed.TotalDays:0} days ago"),
			_ => moment.ToLocalTime().ToString("MMM d", CultureInfo.CurrentCulture),
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
		span = TimeSpan.FromMinutes(Math.Round(span.TotalMinutes, MidpointRounding.AwayFromZero));
		if (span < TimeSpan.FromHours(1))
		{
			return FormattableString.CurrentCulture($"in {span.Minutes} min");
		}
		if (span < TimeSpan.FromDays(1))
		{
			return span.Minutes == 0
				? FormattableString.CurrentCulture($"in {span.Hours} hr")
				: FormattableString.CurrentCulture($"in {span.Hours} hr {span.Minutes} min");
		}
		return span.Days == 1 ? "tomorrow" : FormattableString.CurrentCulture($"in {span.Days} days");
	}

	internal static string AwayExactly(DateTimeOffset at, DateTimeOffset from)
	{
		var span = at - from;

		return span switch
		{
			{ TotalSeconds: < 1 } => "due now",
			{ TotalMinutes: < 1 } => FormattableString.CurrentCulture($"in {span.Seconds} s"),
			{ TotalHours: < 1 } => FormattableString.CurrentCulture($"in {span.Minutes} min {span.Seconds} s"),
			_ => FormattableString.CurrentCulture($"in {(int)span.TotalHours} hr {span.Minutes} min {span.Seconds} s"),
		};
	}
}
