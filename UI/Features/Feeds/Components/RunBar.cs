using System.Globalization;
using Domain.Feed.Run;

namespace UI.Features.Feeds.Components;

internal enum RunPart
{
	Known,
	NotFoundEarlier,
	Matched,
	NotFound,
	CouldNotLookUp,
}

internal readonly record struct RunSegment(RunPart Part, int Count, double Percent)
{
	internal string Style => string.Create(CultureInfo.InvariantCulture, $"width: {Percent:0.###}%; background: {RunBar.Color(Part)}");
}

internal static class RunBar
{
	private static readonly RunPart[] LegendOrder = [RunPart.Matched, RunPart.Known, RunPart.NotFound, RunPart.NotFoundEarlier, RunPart.CouldNotLookUp];

	internal static IReadOnlyList<RunSegment> Segments(FeedRunCounts counts)
	{
		var matched = counts.Total - counts.Unmatched;
		(RunPart Part, int Count)[] parts =
		[
			(RunPart.Known, counts.Known),
			(RunPart.NotFoundEarlier, counts.NotFoundEarlier),
			(RunPart.Matched, matched - counts.Known),
			(RunPart.NotFound, counts.UnmatchedNotFound - counts.NotFoundEarlier),
			(RunPart.CouldNotLookUp, counts.UnmatchedCouldNotLookUp),
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
		};
	}

	internal static string Color(RunPart part)
	{
		return part switch
		{
			RunPart.Known => "var(--usage-answered)",
			RunPart.NotFoundEarlier => "var(--usage-shade)",
			RunPart.Matched => "var(--alert-success)",
			RunPart.NotFound => "var(--destructive)",
			RunPart.CouldNotLookUp => "var(--usage-gave-up)",
		};
	}
}
