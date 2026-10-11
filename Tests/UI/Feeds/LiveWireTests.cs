using Domain.Feed.Trace;
using Domain.Providers;
using Shared.Concurrency;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class LiveWireTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task Step_ForAConflictedTitle_SendsTheConflictFate()
	{
		var step = LiveWire.Step(new TraceStep(7, 120, Some.Of(3), new TitleSettled(TitleFate.Conflict)));

		await Assert.That(step.Fate).IsEqualTo((byte)TitleFate.Conflict);
	}

	[Test]
	public async Task Capacity_ForALaneWithWaiters_ReportsItBusy()
	{
		var idle = LiveWire.Capacity(RemoteService.Tmdb, RemoteLoad.Idle, Now, 0);
		var waiting = LiveWire.Capacity(RemoteService.Tmdb, RemoteLoad.Idle with { Waiting = 3, EstimatedWait = TimeSpan.FromSeconds(2) }, Now, 0);

		await Assert.That(idle.Busy).IsEqualTo(0);
		await Assert.That(waiting.Busy).IsBetween(0.8, 1);
	}

	[Test]
	public async Task Capacity_ForAPacedLoad_CarriesTheOpeningAndTheWindow()
	{
		var load = RemoteLoad.Idle with { Waiting = 2614, Paced = Some.Of(new LanePace(Now.AddSeconds(34), 300, TimeSpan.FromMinutes(1))) };

		var capacity = LiveWire.Capacity(RemoteService.Plex, load, Now, 412000);

		await Assert.That(capacity.PacedUntilMs).IsEqualTo(446000);
		await Assert.That(capacity.PacedRequests).IsEqualTo(300);
		await Assert.That(capacity.PacedPerMs).IsEqualTo(60000);
		await Assert.That(capacity.BlockedUntilMs).IsEqualTo(0);
	}
}
