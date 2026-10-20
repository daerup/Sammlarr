using System.Globalization;
using Contracts.Datasets;
using Contracts.Housekeeping;
using Contracts.Media;
using Domain.Providers;
using Shared.Options;
using TestInfrastructure;
using UI.Features.Settings.Components.Housekeeping;
using UI.Shared.Bars;

namespace UI.Tests.Settings.Housekeeping;

public class HousekeepingViewTests
{
	private static readonly DateTimeOffset Now = new(new DateTime(2026, 10, 11, 9, 0, 0, DateTimeKind.Local));

	[Test]
	[Arguments(-1, "yesterday 04:30")]
	[Arguments(0, "today 04:30")]
	[Arguments(1, "tomorrow 04:30")]
	[Arguments(-3, "Oct 8 04:30")]
	public async Task Day_RelativeToToday_NamesTheDayOnlyWhenItIsNear(int days, string expected)
	{
		var at = new DateTimeOffset(new DateTime(2026, 10, 11, 4, 30, 0, DateTimeKind.Local).AddDays(days));

		await Assert.That(HousekeepingView.Day(at, Now)).IsEqualTo(expected);
	}

	[Test]
	[Arguments(34_288_435L, "32.7 MB")]
	[Arguments(113_246_208L, "108 MB")]
	public async Task Size_InAnyCulture_ReadsInTheLargestWholeUnitWithOneDecimal(long bytes, string expected)
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

		await Assert.That(HousekeepingView.Size(bytes)).IsEqualTo(expected);
	}

	[Test]
	[Arguments(108, 50, true)]
	[Arguments(33, 31, false)]
	public async Task Bloated_ForTheFileAgainstItsData_IsTrueFromHalfAgainTheData(long onDisk, long data, bool expected)
	{
		await Assert.That(HousekeepingView.Bloated(new DatabaseSize(onDisk, data))).IsEqualTo(expected);
	}

	[Test]
	public async Task File_WhenTheEstimatesPassTheData_LeavesNothingForTheRestAndCountsFreeSpaceApart()
	{
		var pass = new HousekeepingReport(Now, Some.Of(Now), [], new None(), new None());
		var overview = Overview(pass, Imdb(new None(), new None())) with { Size = new DatabaseSize(100, 90), Parts = new DatabaseParts(80, 20, 0) };

		var file = HousekeepingView.FileParts(overview, "#F5C518");

		await Assert.That(file[^2]).IsEqualTo(new FilePart("Settings, jobs and usage", 0, new None(), BarPattern.Solid));
		await Assert.That(file[^1]).IsEqualTo(new FilePart("Free pages and write-ahead log", 10, new None(), BarPattern.Hatched));
	}

	[Test]
	public async Task CompactedAt_ForAPassAndADatasetLoad_IsTheLaterOfTheTwo()
	{
		var pass = new HousekeepingReport(Now.AddHours(-6), Some.Of(Now.AddHours(-6)), [], Some.Of(new Compaction(2, 1, false)), new None());
		var load = new DatasetLoad(DatasetNames.ImdbRatings, Now.AddHours(-4), new None(), 1_718_755);
		var unchanged = new DatasetCheck(DatasetNames.ImdbRatings, Now.AddHours(-1), Now.AddHours(-1), DatasetOutcome.Unchanged, new None());

		var compacted = HousekeepingView.CompactedAt(Overview(pass, Imdb(Some.Of(unchanged), Some.Of(load))));

		await Assert.That(compacted).IsSome().And.IsEqualTo(Now.AddHours(-4));
	}

	[Test]
	public async Task Breakdown_OfAPass_NamesEachStepWithItsWindowAndJoinsJobsAndScheduleEntries()
	{
		var report = new HousekeepingReport(
			Now,
			Some.Of(Now),
			[
				new StepCount(HousekeepingStep.RunHistory, 500),
				new StepCount(HousekeepingStep.TitleIds, 11),
				new StepCount(HousekeepingStep.ShowParts, 4),
				new StepCount(HousekeepingStep.SettledJobs, 200),
				new StepCount(HousekeepingStep.ScheduleEntries, 26),
			],
			new None(),
			new None()
		);

		await Assert
			.That(HousekeepingView.Breakdown(report, new HousekeepingOptions()))
			.IsEquivalentTo([
				("Feed history: titles and calls of runs older than 90 days", 500),
				("Title ids nobody used for 365 days", 11),
				("\"No earlier season\" answers older than 3 days", 4),
				("Finished jobs and schedule entries, 30 days", 226),
			]);
	}

	[Test]
	public async Task Removed_ForTodaysPass_NamesItAsTodays()
	{
		var report = new HousekeepingReport(Now.AddHours(-6), Some.Of(Now.AddHours(-6)), [], new None(), new None());

		await Assert.That(HousekeepingView.Removed(report, Now)).IsEqualTo("Removed by today's pass, 03:00");
	}

	[Test]
	public async Task Dataset_WhenTheFileWasUnchanged_ShowsTheRowsItHoldsAsUnchanged()
	{
		var check = new DatasetCheck(DatasetNames.ImdbRatings, Now, Now, DatasetOutcome.Unchanged, new None());
		var loaded = new DatasetLoad(DatasetNames.ImdbRatings, Now.AddDays(-2), new None(), 1_718_755);

		var line = HousekeepingView.Dataset(Imdb(Some.Of(check), Some.Of(loaded)), Now);

		await Assert.That(line).IsEqualTo(new TaskLine(TaskTone.Quiet, "today 09:00 · unchanged", Some.Of("1,718,755"), "ratings · unchanged", new None()));
	}

	[Test]
	public async Task Dataset_LoadedBeforeAnyCheckWasKept_ShowsThatLoad()
	{
		var loaded = new DatasetLoad(DatasetNames.ImdbRatings, Now.AddDays(-2), new None(), 1_718_755);

		var line = HousekeepingView.Dataset(Imdb(new None(), Some.Of(loaded)), Now);

		await Assert.That(line).IsEqualTo(new TaskLine(TaskTone.Ok, "Oct 9 09:00 · loaded", Some.Of("1,718,755"), "ratings", new None()));
	}

	[Test]
	public async Task Pass_WhoseReportWasStoredInTheSameTenthOfAMillisecondItStarted_ShowsItsSteps()
	{
		var since = Now.AddTicks(1_234);
		var stored = Now.AddTicks(1_000);
		var report = new HousekeepingReport(stored, new None(), [new StepCount(HousekeepingStep.RunHistory, 500)], new None(), new None());

		var line = HousekeepingView.Pass(new PassTask(new None(), Some.Of(new TaskRun(since, true)), Some.Of(report)), Now);

		await Assert.That(line.Did).IsEqualTo("1 of 8 steps done");
	}

	[Test]
	public async Task Pass_WhoseOnlyReportIsAnEarlierUnfinishedOne_WaitsForItsTurn()
	{
		var report = new HousekeepingReport(Now.AddDays(-1), new None(), [new StepCount(HousekeepingStep.RunHistory, 500)], new None(), new None());

		var line = HousekeepingView.Pass(new PassTask(new None(), Some.Of(new TaskRun(Now, true)), Some.Of(report)), Now);

		await Assert.That(line.Did).IsEqualTo("waiting for its turn");
	}

	[Test]
	public async Task Dataset_ClaimedButNotStarted_SaysItIsQueuedAndWaitingForItsTurn()
	{
		var queued = new DatasetTask(RemoteService.ImdbDatasets, new None(), Some.Of(new TaskRun(Now, false)), new None(), new None());

		var line = HousekeepingView.Dataset(queued, Now);

		await Assert.That(line).IsEqualTo(new TaskLine(TaskTone.Running, "queued at 09:00", new None(), "waiting for its turn", new None()));
	}

	private static DatasetTask Imdb(Optional<DatasetCheck> last, Optional<DatasetLoad> loaded)
	{
		return new DatasetTask(RemoteService.ImdbDatasets, new None(), new None(), last, loaded);
	}

	private static HousekeepingOverview Overview(HousekeepingReport pass, DatasetTask imdb)
	{
		return new HousekeepingOverview(
			new DatabaseSize(2, 1),
			new DatabaseParts(0, 0, 0),
			new PassTask(new None(), new None(), Some.Of(pass)),
			[imdb],
			new CachedTitles(0, 0, 0, 0, 0, 0),
			new StoredTwiceCount(0, 0)
		);
	}
}
