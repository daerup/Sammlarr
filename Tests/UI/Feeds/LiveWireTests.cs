using Domain.Feed.Trace;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class LiveWireTests
{
	[Test]
	public async Task Step_ForAConflictedTitle_SendsTheConflictFate()
	{
		var step = LiveWire.Step(new TraceStep(7, 120, Some.Of(3), new TitleSettled(TitleFate.Conflict)));

		await Assert.That(step.Fate).IsEqualTo((byte)TitleFate.Conflict);
	}
}
