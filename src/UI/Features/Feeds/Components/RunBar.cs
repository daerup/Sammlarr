using System.Globalization;
using Domain.Feed.Run;

namespace UI.Features.Feeds.Components;

internal enum RunPart
{
	Known,
	Matched,
	NotFound,
	NotFoundEarlier,
	CouldNotLookUp,
	Conflict,
}

internal readonly record struct RunSegment(RunPart Part, int Count, double Percent)
{
	internal string Style => string.Create(CultureInfo.InvariantCulture, $"width: {Percent:0.###}%; background: {RunBar.Color(Part)}");
}

internal static class RunBar
{
	private static readonly RunPart[] LegendOrder =
	[
		RunPart.Matched,
		RunPart.Known,
		RunPart.NotFound,
		RunPart.NotFoundEarlier,
		RunPart.CouldNotLookUp,
		RunPart.Conflict,
	];

	internal static IReadOnlyList<RunSegment> Segments(FeedRunCounts counts)
	{
		var matched = counts.Total - counts.Unmatched;
		(RunPart Part, int Count)[] parts =
		[
			(RunPart.Known, counts.Known),
			(RunPart.Matched, matched - counts.Known),
			(RunPart.NotFound, counts.UnmatchedNotFound - counts.NotFoundEarlier),
			(RunPart.NotFoundEarlier, counts.NotFoundEarlier),
			(RunPart.CouldNotLookUp, counts.UnmatchedCouldNotLookUp),
			(RunPart.Conflict, counts.UnmatchedConflict),
		];
		return [.. parts.Where(part => part.Count > 0).Select(part => new RunSegment(part.Part, part.Count, 100.0 * part.Count / counts.Total))];
	}

	internal static IReadOnlyList<RunSegment> Legend(IReadOnlyList<RunSegment> segments) =>
		[.. segments.OrderBy(segment => Array.IndexOf(LegendOrder, segment.Part))];

	internal static string Label(RunPart part)
	{
		return part switch
		{
			RunPart.Known => "Already known",
			RunPart.NotFoundEarlier => "Not found earlier",
			RunPart.Matched => "Matched",
			RunPart.NotFound => "Not found",
			RunPart.CouldNotLookUp => "Couldn't look up",
			RunPart.Conflict => "Conflict",
		};
	}

	internal const string InFlightMeaning = "Being looked up right now.";

	internal static string Meaning(RunPart part, string sink)
	{
		return part switch
		{
			RunPart.Known => "Sammlarr already had this title from an earlier run, so no service was asked.",
			RunPart.Matched => $"Found this run: Sammlarr now has the ids it needs to add it to {sink}.",
			RunPart.NotFound => "Every service that could know this title was asked and none had it, or the list gave no id to look it up by.",
			RunPart.NotFoundEarlier => "A lookup in the last hour found nothing, so Sammlarr did not ask again. After the hour it is looked up again.",
			RunPart.CouldNotLookUp => "A service this title needed was down, rate-limited or failing, so the lookup never finished. The next run tries again.",
			RunPart.Conflict => "Its ids disagree with titles Sammlarr already holds, so it was kept apart and not added. The titles stay as they are.",
		};
	}

	internal static string Color(RunPart part)
	{
		return part switch
		{
			RunPart.Known => "var(--usage-answered)",
			RunPart.NotFoundEarlier => "var(--fate-earlier)",
			RunPart.Matched => "var(--alert-success)",
			RunPart.NotFound => "var(--destructive)",
			RunPart.CouldNotLookUp => "var(--fate-couldnot)",
			RunPart.Conflict => "var(--fate-conflict)",
		};
	}
}
