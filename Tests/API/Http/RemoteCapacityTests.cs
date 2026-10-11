using API.Http;
using API.Http.Lanes;
using Contracts;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shared.Concurrency;
using Shared.Options;

namespace API.Tests.Http;

public class RemoteCapacityTests : IDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

	private readonly FakeTimeProvider _time = new(Now);
	private readonly Lanes _lanes;
	private readonly RemoteCapacity Testee;

	public RemoteCapacityTests()
	{
		var http = new ProviderHttpOptions
		{
			MaxWaitSeconds = 90,
			EnrichWaitSeconds = 15,
			Lanes = new Dictionary<string, LaneOptions>
			{
				["api"] = new() { Limits = [new RateWindowOptions { Requests = 1, Per = TimeSpan.FromSeconds(1) }], FailuresToDown = 1 },
			},
		};
		_lanes = new Lanes(_time, [new RemoteSettings(RemoteService.Plex, LanedHosts.Plex(http))], new ServiceCollection().BuildServiceProvider());
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(Now);
		Testee = new RemoteCapacity(_lanes, clock.Object);
	}

	public void Dispose() => _lanes.Dispose();

	[Test]
	public async Task Of_AfterAThrottleWithRetryAfter_StaysBlockedUntilThen()
	{
		await ReleaseAsync(new AttemptThrottled(Some.Of(Now.AddSeconds(20))));

		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(load.IsOpen(Now)).IsFalse();
		await Assert.That(load.IsOpen(Now.AddSeconds(20))).IsTrue();
	}

	[Test]
	public async Task Of_AfterAnAnswer_IsOpen()
	{
		await ReleaseAsync(new AttemptAnswered());

		await Assert.That(Testee.Of(RemoteService.Plex).IsOpen(Now)).IsTrue();
	}

	[Test]
	public async Task Of_WhileARouteIsDown_IsBlockedUntilTheDownEnds()
	{
		await ReleaseAsync(new AttemptFailed());

		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(load.IsOpen(Now.AddSeconds(29))).IsFalse();
		await Assert.That(load.IsOpen(Now.AddSeconds(30))).IsTrue();
	}

	[Test]
	public async Task Of_WhileAskersWaitForTheWindow_ReportsTheRemoteBusy()
	{
		var gate = _lanes.Gate(new LaneName(RemoteService.Plex, "api"), new None());
		await gate.EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(1), CancellationToken.None);
		var waiting = gate.EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(1), CancellationToken.None);

		await Assert.That(Testee.Of(RemoteService.Plex).Busy).IsEqualTo(0.5);
		await Assert.That(waiting.IsCompleted).IsFalse();
	}

	[Test]
	public async Task Of_ForAThrottledGateOfOneKey_StaysOpen()
	{
		var gate = _lanes.Gate(new LaneName(RemoteService.Plex, "api", "plex-token"), new None());
		var permit = (await gate.EnterAsync(LanePriority.Interactive, TimeSpan.Zero, CancellationToken.None)).GetOrThrow(_ => new InvalidOperationException(
			"refused"
		));

		permit.Release(new AttemptThrottled(Some.Of(Now.AddMinutes(5))));

		await Assert.That(Testee.Of(RemoteService.Plex).IsOpen(Now)).IsTrue();
	}

	[Test]
	public async Task Of_WhenBlockedByHand_IsBlockedForThatLong()
	{
		Testee.Block(RemoteService.Plex, TimeSpan.FromMinutes(1));

		await Assert.That(Testee.Of(RemoteService.Plex).IsOpen(Now.AddSeconds(59))).IsFalse();
	}

	[Test]
	public async Task Of_ForARemoteWithWaitBudgets_ReportsBoth()
	{
		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(load.MaxWait).IsEqualTo(TimeSpan.FromSeconds(90));
		await Assert.That(load.EnrichWait).IsEqualTo(TimeSpan.FromSeconds(15));
	}

	private async Task ReleaseAsync(AttemptOutcome outcome)
	{
		var gate = _lanes.Gate(new LaneName(RemoteService.Plex, "api"), new None());
		var entered = await gate.EnterAsync(LanePriority.Interactive, TimeSpan.Zero, CancellationToken.None);
		entered.GetOrThrow(_ => new InvalidOperationException("The gate refused a permit it should grant.")).Release(outcome);
	}
}
