using Domain.Feed.Run;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class RunBarTests
{
	[Test]
	public async Task Segments_ForARun_SumToItsTotal()
	{
		var counts = FeedRunCounts.None with { Total = 20, Known = 6, NotFoundEarlier = 2, UnmatchedNotFound = 5, UnmatchedCouldNotLookUp = 1 };

		var segments = RunBar.Segments(counts);

		await Assert
			.That(segments.Select(segment => (segment.Part, segment.Count)))
			.IsEquivalentTo(
				[(RunPart.Known, 6), (RunPart.Matched, 8), (RunPart.NotFound, 3), (RunPart.NotFoundEarlier, 2), (RunPart.CouldNotLookUp, 1)],
				CollectionOrdering.Matching
			);
		await Assert
			.That(RunBar.Legend(segments).Select(segment => segment.Part))
			.IsEquivalentTo([RunPart.Matched, RunPart.Known, RunPart.NotFound, RunPart.NotFoundEarlier, RunPart.CouldNotLookUp], CollectionOrdering.Matching);
		await Assert.That(segments.Sum(segment => segment.Percent)).IsEqualTo(100);
	}

	[Test]
	public async Task Meaning_ForMatched_NamesTheSink()
	{
		await Assert.That(RunBar.Meaning(RunPart.Matched, "Radarr + Sonarr")).Contains("Radarr + Sonarr");
	}

	[Test]
	public async Task Meaning_ForEveryPart_SaysSomething()
	{
		await Assert.That(Enum.GetValues<RunPart>().Select(part => RunBar.Meaning(part, "Plex")).Distinct().Count()).IsEqualTo(Enum.GetValues<RunPart>().Length);
	}

	[Test]
	public async Task Segments_ForAnEmptyRun_AreNone()
	{
		await Assert.That(RunBar.Segments(FeedRunCounts.None)).IsEmpty();
	}
}
