using CronExpressionDescriptor;
using Domain.Feed;
using Humanizer;
using UI.Shared.Formatting;

namespace UI.Features.Feeds.Components;

internal static class ScheduleText
{
	private static readonly Options English = new() { Locale = "en" };

	internal static string Describe(FeedRunTrigger schedule)
	{
		return schedule is Scheduled scheduled ? ExpressionDescriptor.GetDescription(scheduled.ToString(), English) : "Manual";
	}

	internal static string Away(DateTimeOffset at, DateTimeOffset from) => Due(at - from) ? "due now" : at.Humanize(from);

	internal static string AwayExactly(DateTimeOffset at, DateTimeOffset from)
	{
		var span = at - from;
		return Due(span) ? "due now" : $"in {span.Humanize(4, Notation.Symbols, TimeUnit.Second)}";
	}

	private static bool Due(TimeSpan left) => left < TimeSpan.FromSeconds(1);
}
