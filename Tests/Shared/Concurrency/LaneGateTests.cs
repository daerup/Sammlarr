using Microsoft.Extensions.Time.Testing;
using Shared.Concurrency;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Shared.Tests.Concurrency;

public class LaneGateTests
{
	private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

	private static readonly RateWindow OnePerSecond = new(1, TimeSpan.FromSeconds(1));

	private static readonly RateWindow TwoPerSecond = new(2, TimeSpan.FromSeconds(1));

	private static readonly RateWindow FourPerSecond = new(4, TimeSpan.FromSeconds(1));

	[Test]
	public async Task EnterAsync_PastTheWindow_WaitsForTheOldestGrantToAge()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(TwoPerSecond), time, new None(), "");
		await GrantedAsync(testee);
		await GrantedAsync(testee);

		var third = Enter(testee, LanePriority.Interactive);
		var grantedAtOnce = third.IsCompleted;
		time.Advance(TimeSpan.FromSeconds(1));

		await Assert.That(grantedAtOnce).IsFalse();
		await Assert.That(await third.WaitAsync(Patience)).IsSuccess();
	}

	[Test]
	public async Task EnterAsync_WithWaitersOfBothClasses_GrantsInteractiveThenOldestTitle()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(OnePerSecond), time, new None(), "");
		await GrantedAsync(testee);
		var later = Enter(testee, new LanePriority(LaneClass.Title, 2));
		var older = Enter(testee, new LanePriority(LaneClass.Title, 1));
		var search = Enter(testee, LanePriority.Interactive);
		var grantedAfterEachSecond = new List<string>();

		for (var second = 0; second < 3; second++)
		{
			time.Advance(TimeSpan.FromSeconds(1));
			grantedAfterEachSecond.Add(string.Concat(new[] { search, older, later }.Select(entering => entering.IsCompleted ? '1' : '0')));
		}

		await Assert.That(grantedAfterEachSecond).IsEquivalentTo(["100", "110", "111"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task EnterAsync_WhileBlockedPastThePatience_IsRefused()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(TwoPerSecond), time, new None(), "");
		(await GrantedAsync(testee)).Release(new AttemptThrottled(Some.Of(time.GetUtcNow().AddMinutes(5))));

		var refused = await testee.EnterAsync(LanePriority.Interactive, TimeSpan.FromSeconds(15), CancellationToken.None);

		await Assert.That(refused).IsError();
	}

	[Test]
	public async Task EnterAsync_WhileBlockedWithinThePatience_GrantsWhenTheBlockEnds()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(TwoPerSecond), time, new None(), "");
		(await GrantedAsync(testee)).Release(new AttemptThrottled(Some.Of(time.GetUtcNow().AddSeconds(10))));

		var waiting = testee.EnterAsync(LanePriority.Interactive, TimeSpan.FromSeconds(15), CancellationToken.None);
		var grantedAtOnce = waiting.IsCompleted;
		time.Advance(TimeSpan.FromSeconds(10));

		await Assert.That(grantedAtOnce).IsFalse();
		await Assert.That(await waiting.WaitAsync(Patience)).IsSuccess();
	}

	[Test]
	public async Task Release_ThrottledWithRetryAfter_RefusesTheQueuedWaitersItOutlasts()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(OnePerSecond), time, new None(), "");
		var first = await GrantedAsync(testee);
		var patient = testee.EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(1), CancellationToken.None);
		var impatient = testee.EnterAsync(LanePriority.Interactive, TimeSpan.FromSeconds(5), CancellationToken.None);

		first.Release(new AttemptThrottled(Some.Of(time.GetUtcNow().AddSeconds(30))));

		await Assert.That(await impatient.WaitAsync(Patience)).IsError();
		await Assert.That(patient.IsCompleted).IsFalse();
	}

	[Test]
	public async Task Release_FailingTwice_GoesDownThenLetsOneProbeThrough()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(TwoPerSecond), time, new None(), "");
		await FailTwiceAsync(testee);

		var whileDown = await testee.EnterAsync(LanePriority.Interactive, TimeSpan.Zero, CancellationToken.None);
		time.Advance(TimeSpan.FromSeconds(30));
		var probe = await testee.EnterAsync(LanePriority.Interactive, TimeSpan.Zero, CancellationToken.None);
		var second = Enter(testee, LanePriority.Interactive);

		await Assert.That(whileDown).IsError();
		await Assert.That(probe).IsSuccess();
		await Assert.That(second.IsCompleted).IsFalse();
	}

	[Test]
	public async Task Load_WhileDown_IsBlockedUntilTheDownEnds()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(TwoPerSecond), time, new None(), "");
		await FailTwiceAsync(testee);

		await Assert.That(testee.Load.BlockedUntil).IsSome().And.IsEqualTo(time.GetUtcNow().AddSeconds(30));
	}

	[Test]
	public async Task Admits_WhileBlockedPastThePatience_IsFalseAndWithinItTrue()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(TwoPerSecond), time, new None(), "");
		(await GrantedAsync(testee)).Release(new AttemptThrottled(Some.Of(time.GetUtcNow().AddSeconds(10))));

		await Assert.That(testee.Admits(LanePriority.Interactive, TimeSpan.FromSeconds(5))).IsFalse();
		await Assert.That(testee.Admits(LanePriority.Interactive, TimeSpan.FromSeconds(15))).IsTrue();
	}

	[Test]
	public async Task Release_WhenTheProbeIsAnswered_OpensTheGateAgain()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(TwoPerSecond), time, new None(), "");
		await FailTwiceAsync(testee);
		time.Advance(TimeSpan.FromSeconds(30));
		var probe = await GrantedAsync(testee);
		var second = Enter(testee, LanePriority.Interactive);

		probe.Release(new AttemptAnswered());

		await Assert.That(await second.WaitAsync(Patience)).IsSuccess();
		await Assert.That(testee.Load.Down).IsFalse();
	}

	[Test]
	public async Task Release_WhenTheProbeFails_GoesDownAgain()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(TwoPerSecond), time, new None(), "");
		await FailTwiceAsync(testee);
		time.Advance(TimeSpan.FromSeconds(30));
		var probe = await GrantedAsync(testee);

		probe.Release(new AttemptFailed());

		await Assert.That(testee.Load.Down).IsTrue();
		await Assert.That(await testee.EnterAsync(LanePriority.Interactive, TimeSpan.Zero, CancellationToken.None)).IsError();
	}

	[Test]
	public async Task Release_ThrottledWithoutRetryAfter_HalvesTheWindowOncePerWindow()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(FourPerSecond), time, new None(), "");
		var first = await GrantedAsync(testee);
		var second = await GrantedAsync(testee);
		first.Release(new AttemptThrottled(new None()));
		second.Release(new AttemptThrottled(new None()));
		time.Advance(TimeSpan.FromSeconds(1));

		var granted = Enumerable.Range(0, 3).Select(_ => Enter(testee, LanePriority.Interactive)).ToList();

		await Assert.That(granted.Select(entering => entering.IsCompleted)).IsEquivalentTo([true, true, false], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Release_AfterACleanWindow_AddsOneRequest()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(FourPerSecond), time, new None(), "");
		(await GrantedAsync(testee)).Release(new AttemptThrottled(new None()));
		time.Advance(TimeSpan.FromSeconds(1));
		(await GrantedAsync(testee)).Release(new AttemptAnswered());
		time.Advance(TimeSpan.FromSeconds(1));

		var granted = Enumerable.Range(0, 4).Select(_ => Enter(testee, LanePriority.Interactive)).ToList();

		await Assert.That(granted.Select(entering => entering.IsCompleted)).IsEquivalentTo([true, true, true, false], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Release_AfterAWindowWithAThrottle_AddsNothing()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(FourPerSecond), time, new None(), "");
		var first = await GrantedAsync(testee);
		var second = await GrantedAsync(testee);
		first.Release(new AttemptThrottled(new None()));
		time.Advance(TimeSpan.FromMilliseconds(500));
		second.Release(new AttemptThrottled(new None()));
		time.Advance(TimeSpan.FromMilliseconds(700));
		(await GrantedAsync(testee)).Release(new AttemptAnswered());
		time.Advance(TimeSpan.FromMilliseconds(1100));

		var granted = Enumerable.Range(0, 3).Select(_ => Enter(testee, LanePriority.Interactive)).ToList();

		await Assert.That(granted.Select(entering => entering.IsCompleted)).IsEquivalentTo([true, true, false], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Release_ThrottledSoonAfterAnIncrease_StillHalves()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(FourPerSecond), time, new None(), "");
		(await GrantedAsync(testee)).Release(new AttemptThrottled(new None()));
		time.Advance(TimeSpan.FromSeconds(1));
		(await GrantedAsync(testee)).Release(new AttemptAnswered());
		time.Advance(TimeSpan.FromMilliseconds(500));
		(await GrantedAsync(testee)).Release(new AttemptThrottled(new None()));
		time.Advance(TimeSpan.FromMilliseconds(1100));

		var granted = Enumerable.Range(0, 2).Select(_ => Enter(testee, LanePriority.Interactive)).ToList();

		await Assert.That(granted.Select(entering => entering.IsCompleted)).IsEquivalentTo([true, false], CollectionOrdering.Matching);
	}

	[Test]
	public async Task EnterAsync_AtTheQuotaReserve_HoldsWhatTheQuotaBlocksForTheAskingClass()
	{
		var time = new FakeTimeProvider();
		var quota = ILaneQuota.Mock();
		quota.BlockedUntil(Any(), Any(), Any()).Returns(new None());
		quota.BlockedUntil(Any(), LaneClass.Title, Any()).Returns(Some.Of(time.GetUtcNow().AddHours(1)));
		using var testee = new LaneGate(Policy(TwoPerSecond), time, Some.Of(quota.Object), "mdblist-key");

		var source = await testee.EnterAsync(new LanePriority(LaneClass.Source, 1), TimeSpan.Zero, CancellationToken.None);
		var title = await testee.EnterAsync(new LanePriority(LaneClass.Title, 2), TimeSpan.Zero, CancellationToken.None);

		await Assert.That(source).IsSuccess();
		await Assert.That(title).IsError();
		quota.BlockedUntil("mdblist-key", LaneClass.Source, Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task EnterAsync_WhenAReserveStartsWhileATitleWaits_RefusesItOnceItOutlastsItsPatience()
	{
		var time = new FakeTimeProvider();
		Optional<DateTimeOffset> reserve = new None();
		var quota = ILaneQuota.Mock();
		quota.BlockedUntil(Any(), Any(), Any()).Returns((_, _, _) => reserve);
		using var testee = new LaneGate(Policy(OnePerSecond), time, Some.Of(quota.Object), "mdblist-key");
		await GrantedAsync(testee);
		var title = testee.EnterAsync(new LanePriority(LaneClass.Title, 1), TimeSpan.FromSeconds(15), CancellationToken.None);

		reserve = Some.Of(time.GetUtcNow().AddHours(1));
		time.Advance(TimeSpan.FromSeconds(1));

		await Assert.That(await title.WaitAsync(Patience)).IsError();
	}

	[Test]
	public async Task EnterAsync_WhenAWaiterIsCancelled_LeavesTheQueue()
	{
		var time = new FakeTimeProvider();
		using var testee = new LaneGate(Policy(OnePerSecond), time, new None(), "");
		await GrantedAsync(testee);
		using var cancellation = new CancellationTokenSource();
		var cancelled = testee.EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(1), cancellation.Token);

		await cancellation.CancelAsync();

		await Assert.That(async () => await cancelled).Throws<OperationCanceledException>();
		await Assert.That(testee.Load.Waiting).IsEqualTo(0);
	}

	[Test]
	public async Task EnterAsync_OnAnAlignedWindow_OpensAtTheBoundary()
	{
		var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 3, 0, TimeSpan.Zero));
		using var testee = new LaneGate(Policy(new RateWindow(1, TimeSpan.FromMinutes(5), Aligned: true)), time, new None(), "");
		await GrantedAsync(testee);

		var next = testee.EnterAsync(LanePriority.Interactive, TimeSpan.FromMinutes(10), CancellationToken.None);
		var grantedAtOnce = next.IsCompleted;
		time.Advance(TimeSpan.FromMinutes(2));

		await Assert.That(grantedAtOnce).IsFalse();
		await Assert.That(await next.WaitAsync(Patience)).IsSuccess();
	}

	private static LanePolicy Policy(params RateWindow[] limits) => new(limits, int.MaxValue, 2, TimeSpan.FromSeconds(30));

	private static Task<Result<LanePermit>> Enter(LaneGate gate, LanePriority priority)
	{
		return gate.EnterAsync(priority, TimeSpan.FromMinutes(1), CancellationToken.None);
	}

	private static async Task<LanePermit> GrantedAsync(LaneGate gate)
	{
		var entered = await gate.EnterAsync(LanePriority.Interactive, TimeSpan.Zero, CancellationToken.None);
		return entered.GetOrThrow(_ => new InvalidOperationException("The gate refused a permit it should grant."));
	}

	private static async Task FailTwiceAsync(LaneGate gate)
	{
		for (var attempt = 0; attempt < 2; attempt++)
		{
			(await GrantedAsync(gate)).Release(new AttemptFailed());
		}
	}
}
