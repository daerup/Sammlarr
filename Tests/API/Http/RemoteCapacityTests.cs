using API.Http;
using API.Http.Lanes;
using Contracts.Identity;
using Contracts.Providers;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Http;

public class RemoteCapacityTests : IDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

	private static readonly DateTimeOffset Reset = Now.AddHours(1);

	private readonly FakeTimeProvider _time = new(Now);
	private readonly UserId _user = UserId.New();
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
		var quota = ILaneQuota.Mock();
		quota.BlockedUntil(Any(), Any(), Any()).Returns(new None());
		quota.BlockedUntil(Any(), LaneClass.Background, Any()).Returns(Some.Of(Reset));
		var services = new ServiceCollection().AddKeyedSingleton(RemoteService.Plex, quota.Object).BuildServiceProvider();
		_lanes = new Lanes(_time, [new RemoteSettings(RemoteService.Plex, LanedHosts.Plex(http))], services);
		var currentUser = ICurrentUser.Mock();
		currentUser.IsAuthenticated.Returns(true);
		currentUser.Id.Returns(_user);
		Testee = new RemoteCapacity(_lanes, currentUser.Object);
	}

	public void Dispose() => _lanes.Dispose();

	[Test]
	public async Task Of_AfterAThrottleWithRetryAfter_StaysBlockedUntilThen()
	{
		await ReleaseAsync(new LaneName(RemoteService.Plex, "api"), new AttemptThrottled(Some.Of(Now.AddSeconds(20))));

		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(load.IsOpen(Now)).IsFalse();
		await Assert.That(load.IsOpen(Now.AddSeconds(20))).IsTrue();
		await Assert.That(load.EstimatedWait).IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(20));
	}

	[Test]
	public async Task Of_AfterAnAnswer_IsOpen()
	{
		await ReleaseAsync(new LaneName(RemoteService.Plex, "api"), new AttemptAnswered());

		await Assert.That(Testee.Of(RemoteService.Plex).IsOpen(Now)).IsTrue();
	}

	[Test]
	public async Task Of_WhileARouteIsDown_IsDownAndBlockedUntilTheDownEnds()
	{
		await ReleaseAsync(new LaneName(RemoteService.Plex, "api"), new AttemptFailed());

		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(load.Down).IsTrue();
		await Assert.That(load.BlockedUntil).IsSome().And.IsEqualTo(Now.AddSeconds(30));
	}

	[Test]
	public async Task Of_WhileAskersWaitForTheWindow_CountsThemAndTheirWait()
	{
		var gate = _lanes.Gate(new LaneName(RemoteService.Plex, "api"), new None());
		await gate.EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(1), CancellationToken.None);
		var waiting = gate.EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(1), CancellationToken.None);

		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(load.Waiting).IsEqualTo(1);
		await Assert.That(load.EstimatedWait).IsGreaterThan(TimeSpan.Zero);
		await Assert.That(waiting.IsCompleted).IsFalse();
	}

	[Test]
	public async Task Of_ForAPacedLane_CarriesItsOpening()
	{
		await ReleaseAsync(new LaneName(RemoteService.Plex, "api"), new AttemptAnswered());
		var waiting = _lanes
			.Gate(new LaneName(RemoteService.Plex, "api"), new None())
			.EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(1), CancellationToken.None);

		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(waiting.IsCompleted).IsFalse();
		await Assert.That(load.Paced).IsSome().And.IsEqualTo(new LanePace(Now.AddSeconds(1), 1, TimeSpan.FromSeconds(1)));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Of_ForTwoPacedGates_CarriesTheLaterOpening(bool keyedFirst)
	{
		var plain = new LaneName(RemoteService.Plex, "api");
		var keyed = new LaneName(RemoteService.Plex, "api", "plex-token");
		_lanes.Gate(keyed, Some.Of(_user));
		LaneName[] released = keyedFirst ? [keyed, plain] : [plain, keyed];
		await ReleaseAsync(released[0], new AttemptAnswered());
		_time.Advance(TimeSpan.FromMilliseconds(500));
		await ReleaseAsync(released[1], new AttemptAnswered());
		var waiting = new[] { plain, keyed }
			.Select(name => _lanes.Gate(name, new None()).EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(1), CancellationToken.None))
			.ToList();

		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(waiting.Any(entering => entering.IsCompleted)).IsFalse();
		await Assert.That(load.Paced.Map(pace => pace.OpensAt)).IsSome().And.IsEqualTo(Now.AddSeconds(1.5));
	}

	[Test]
	public async Task Of_ForAThrottledGateOfAKeyTheUserNeverSentThrough_StaysOpen()
	{
		await ReleaseAsync(new LaneName(RemoteService.Plex, "api", "plex-token"), new AttemptThrottled(Some.Of(Now.AddMinutes(5))));

		await Assert.That(Testee.Of(RemoteService.Plex).IsOpen(Now)).IsTrue();
	}

	[Test]
	public async Task Of_ForAThrottledGateOfTheCurrentUsersKey_IsBlocked()
	{
		var name = new LaneName(RemoteService.Plex, "api", "plex-token");
		_lanes.Gate(name, Some.Of(_user));

		await ReleaseAsync(name, new AttemptThrottled(Some.Of(Now.AddMinutes(5))));

		await Assert.That(Testee.Of(RemoteService.Plex).IsOpen(Now)).IsFalse();
	}

	[Test]
	public async Task Of_WithNobodySignedIn_LeavesOutTheGatesOfEveryKey()
	{
		var name = new LaneName(RemoteService.Plex, "api", "plex-token");
		_lanes.Gate(name, Some.Of(_user));
		await ReleaseAsync(name, new AttemptThrottled(Some.Of(Now.AddMinutes(5))));

		var anonymous = new RemoteCapacity(_lanes, ICurrentUser.Mock().Object);

		await Assert.That(anonymous.Of(RemoteService.Plex).IsOpen(Now)).IsTrue();
	}

	[Test]
	public async Task Of_UnderBackgroundPriority_ReportsTheBackgroundFloor()
	{
		_lanes.Gate(new LaneName(RemoteService.Plex, "api"), new None());

		RemoteLoad background;
		using (LanePriority.Next(LaneClass.Background).Enter())
		{
			background = Testee.Of(RemoteService.Plex);
		}

		await Assert.That(background.BlockedUntil).IsSome().And.IsEqualTo(Reset);
		await Assert.That(Testee.Of(RemoteService.Plex).BlockedUntil).IsNone();
	}

	[Test]
	public async Task Of_ForARemoteWithWaitBudgets_ReportsBoth()
	{
		var load = Testee.Of(RemoteService.Plex);

		await Assert.That(load.MaxWait).IsEqualTo(TimeSpan.FromSeconds(90));
		await Assert.That(load.EnrichWait).IsEqualTo(TimeSpan.FromSeconds(15));
	}

	private async Task ReleaseAsync(LaneName name, AttemptOutcome outcome)
	{
		var entered = await _lanes.Gate(name, new None()).EnterAsync(LanePriority.Interactive, TimeSpan.Zero, CancellationToken.None);
		entered.GetOrThrow(_ => new InvalidOperationException("The gate refused a permit it should grant.")).Release(outcome);
	}
}
