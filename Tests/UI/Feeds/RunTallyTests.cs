using Domain.Feed.Trace;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class RunTallyTests
{
	[Test]
	public async Task Percent_OfWhatIsDone_IsItsShareOfTheExpectedTotal()
	{
		var tally = RunTally.Of(FateTally.None with { Started = 120, Matched = 100 }, new None(), 4000, new None(), Some.Of(400));

		await Assert.That(tally.Percent(tally.Done)).IsEqualTo(25);
	}
}
