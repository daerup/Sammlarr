using Contracts.Housekeeping;
using Core.Jobs;
using Microsoft.Extensions.Time.Testing;
using TestInfrastructure;

namespace Core.Tests.Jobs;

public class TaskActivityTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private const string Pass = "Housekeeping";

	private TaskActivity Testee { get; }

	private int _changes;

	public TaskActivityTests()
	{
		var clock = new FakeTimeProvider(Now);
		Testee = new TaskActivity(clock);
		Testee.Changed += () => _changes++;
	}

	[Test]
	public async Task TryClaim_WhileTheTaskIsClaimed_IsRefusedAndLeavesItQueued()
	{
		Testee.TryClaim(Pass);

		await Assert.That(Testee.TryClaim(Pass)).IsFalse();
		await Assert.That(Testee.Running(Pass)).IsSome().And.IsEqualTo(new TaskRun(Now, false));
		await Assert.That(_changes).IsEqualTo(1);
	}

	[Test]
	public async Task Begin_AfterAClaim_MarksTheRunStartedThenIdleOnceItEnds()
	{
		Testee.TryClaim(Pass);
		using (Testee.Begin(Pass))
		{
			await Assert.That(Testee.Running(Pass)).IsSome().And.IsEqualTo(new TaskRun(Now, true));
		}

		await Assert.That(Testee.Running(Pass)).IsNone();
		await Assert.That(Testee.TryClaim(Pass)).IsTrue();
		await Assert.That(_changes).IsEqualTo(4);
	}

	[Test]
	public async Task Progressed_WhileATaskRuns_TellsTheListenersWithoutChangingTheRun()
	{
		using var running = Testee.Begin(Pass);

		Testee.Progressed();

		await Assert.That(_changes).IsEqualTo(2);
		await Assert.That(Testee.Running(Pass)).IsSome().And.IsEqualTo(new TaskRun(Now, true));
	}

	[Test]
	public async Task TryClaim_ForAnotherTask_LeavesTheRunningOneAlone()
	{
		Testee.TryClaim(Pass);

		await Assert.That(Testee.TryClaim("imdb-ratings")).IsTrue();
		await Assert.That(Testee.Running(Pass)).IsSome();
	}
}
