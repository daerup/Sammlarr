using Contracts.Housekeeping;
using Core.Housekeeping;
using Core.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.Housekeeping;

public class HousekeepingPassTests : IDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private readonly Mock<IDatabaseFile> _database = IDatabaseFile.Mock();

	private readonly Mock<IHousekeepingReports> _reports = IHousekeepingReports.Mock();

	private readonly Arg<HousekeepingReport> _saved = Any<HousekeepingReport>();

	private readonly JobTurn _turn = new();

	private readonly List<string> _ran = [];

	private readonly TaskActivity _activity;

	private int _progress;

	public HousekeepingPassTests()
	{
		_database.CompactAsync(Any()).Returns(new Compaction(200, 100, true)).Callback(() => _ran.Add("compaction"));
		_reports.SaveAsync(_saved, Any()).Returns(() => Task.CompletedTask);
		var clock = new FakeTimeProvider(Now);
		_activity = new TaskActivity(clock);
		_activity.Changed += () => _progress++;
	}

	public void Dispose() => _turn.Dispose();

	[Test]
	public async Task RunAsync_ForStepsGivenOutOfOrder_RunsEachInItsOrderThenCompacts()
	{
		var testee = Testee(Step(HousekeepingStep.CachedDetails, 3), Step(HousekeepingStep.RunHistory, 5), Step(HousekeepingStep.UsageStats, 2));

		var report = await testee.RunAsync();

		await Assert.That(_ran).IsEquivalentTo(["RunHistory", "UsageStats", "CachedDetails", "compaction"], CollectionOrdering.Matching);
		await Assert
			.That(report.Steps)
			.IsEquivalentTo(
				[new StepCount(HousekeepingStep.RunHistory, 5), new StepCount(HousekeepingStep.UsageStats, 2), new StepCount(HousekeepingStep.CachedDetails, 3)],
				CollectionOrdering.Matching
			);
		await Assert.That(report.File).IsSome().And.IsEqualTo(new Compaction(200, 100, true));
		await Assert.That(report.Failure).IsNone();
	}

	[Test]
	public async Task RunAsync_ForTwoSteps_SavesAndAnnouncesARunningReportAtTheStartAndAfterEachStepThenTheFinishedOne()
	{
		await Testee(Step(HousekeepingStep.RunHistory, 5), Step(HousekeepingStep.UsageStats, 2)).RunAsync();

		var saved = _saved.Values.OfType<HousekeepingReport>().ToList();
		await Assert.That(saved.Select(report => report.Steps.Count)).IsEquivalentTo([0, 1, 2, 2], CollectionOrdering.Matching);
		await Assert.That(saved[0].FinishedAt).IsNone();
		await Assert.That(saved[1].FinishedAt).IsNone();
		await Assert.That(saved[2].FinishedAt).IsNone();
		await Assert.That(saved[^1].FinishedAt).IsSome().And.IsEqualTo(Now);
		await Assert.That(saved[^1].StartedAt).IsEqualTo(Now);
		await Assert.That(_progress).IsEqualTo(3);
	}

	[Test]
	public async Task RunAsync_WhenAStepFails_StopsThereAndRecordsTheFailureBesideWhatWasRemoved()
	{
		var locked = IHousekeepingStep.Mock();
		locked.Step.Returns(HousekeepingStep.UsageStats);
		locked.RunAsync(Any(), Any()).Throws(new InvalidOperationException("database is locked"));
		var later = Step(HousekeepingStep.CachedDetails, 3);

		var report = await Testee(Step(HousekeepingStep.RunHistory, 5), locked.Object, later).RunAsync();

		await Assert.That(report.Steps).IsEquivalentTo([new StepCount(HousekeepingStep.RunHistory, 5)]);
		await Assert
			.That(report.Failure)
			.IsSome()
			.And.Multiple(async failure =>
			{
				await Assert.That(failure.Step).IsSome().And.IsEqualTo(HousekeepingStep.UsageStats);
				await Assert.That(failure.Reason).IsEqualTo("database is locked");
			});
		await Assert.That(_ran).DoesNotContain("CachedDetails");
		_database.CompactAsync(Any()).WasNeverCalled();
	}

	[Test]
	public async Task RunAsync_WhileAJobHoldsTheTurn_StartsOnlyOnceTheJobIsDone()
	{
		var job = await _turn.TakeAsync(CancellationToken.None);

		var pass = Testee(Step(HousekeepingStep.RunHistory, 5)).RunAsync();
		var finishedWhileHeld = pass.IsCompleted;
		var ranWhileHeld = _ran.ToList();
		job.Dispose();
		await pass;

		await Assert.That(finishedWhileHeld).IsFalse();
		await Assert.That(ranWhileHeld).IsEmpty();
		await Assert.That(_ran).IsEquivalentTo(["RunHistory", "compaction"], CollectionOrdering.Matching);
	}

	private IHousekeepingStep Step(HousekeepingStep step, int removed)
	{
		var mock = IHousekeepingStep.Mock();
		mock.Step.Returns(step);
		mock.RunAsync(Any(), Any()).Returns(removed).Callback(() => _ran.Add(step.ToString()));
		return mock.Object;
	}

	private HousekeepingPass Testee(params IHousekeepingStep[] steps)
	{
		var clock = new FakeTimeProvider(Now);
		return new HousekeepingPass(steps, _database.Object, _reports.Object, _turn, _activity, clock, NullLogger<HousekeepingPass>.Instance);
	}
}
