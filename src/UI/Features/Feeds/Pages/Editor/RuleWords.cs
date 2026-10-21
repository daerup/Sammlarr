using System.Globalization;
using Domain.Processing;

namespace UI.Features.Feeds.Pages.Editor;

internal static class RuleWords
{
	public static string Relative(RelativeDate relative)
	{
		if (relative.Amount == 0)
		{
			return "today";
		}

		var unit = relative.Unit switch
		{
			DateUnit.Days => "day",
			DateUnit.Weeks => "week",
			DateUnit.Months => "month",
			DateUnit.Years => "year",
		};
		return $"{relative.Amount} {unit}{(relative.Amount == 1 ? string.Empty : "s")} {(relative.Ahead ? "from now" : "ago")}";
	}

	public static string Day(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

	public static string Short(DurationUnit unit)
	{
		return unit switch
		{
			DurationUnit.Minutes => "min",
			DurationUnit.Hours => "h",
		};
	}

	public static string Runtime(bool total) => total ? "Total runtime" : "Runtime per episode";
}
