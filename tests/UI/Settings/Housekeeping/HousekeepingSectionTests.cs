using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Datasets;
using Contracts.Housekeeping;
using Contracts.Media;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shared.Options;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Housekeeping;
using UI.Shared.Bars;
using UI.Shared.Services;
using UI.Tests.Media;

namespace UI.Tests.Settings.Housekeeping;

public class HousekeepingSectionTests : BunitContext
{
	private static readonly DateTimeOffset Now = new(new DateTime(2026, 10, 11, 9, 0, 0, DateTimeKind.Local));

	private static readonly DateTimeOffset EarlierToday = new(new DateTime(2026, 10, 11, 3, 0, 0, DateTimeKind.Local));

	private readonly Mock<IHousekeepingTasks> _tasks = IHousekeepingTasks.Mock();

	private readonly Mock<IMediaCachePurger> _purger = IMediaCachePurger.Mock();

	private readonly Mock<IUnmatchedMediaCache> _unmatched = IUnmatchedMediaCache.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(new HousekeepingOptions());
		var clock = new FakeTimeProvider(Now);
		Services.AddSingleton<TimeProvider>(clock);
		Services.AddSingleton(_tasks.Object);
		Services.AddSingleton(_purger.Object);
		Services.AddSingleton(_unmatched.Object);
		Services.AddMediaTitleLink();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddSingleton(IConflictReview.Mock().Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_UnderTheSizeLine_BreaksTheFileDownWithAKeyForEachPart()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));

		var cut = RenderLoaded();

		var bar = cut.FindComponent<StackedBar>();
		await Assert.That(bar.Instance.ShowLegend).IsTrue();
		await Assert
			.That(bar.FindComponents<StackedBarPart>().Select(part => (part.Instance.Name, part.Instance.Value)))
			.IsEquivalentTo(
				[
					("IMDb ratings", 22_216_704L),
					("Anime ids", 2_719_744L),
					("Cached details", 3_475_160L),
					("Title ids", 2_605_130L),
					("Show parts", 494_950L),
					("Feed history", 110_592L),
					("Settings, jobs and usage", 477_720L),
					("Free pages and write-ahead log", 2_200_000L),
				],
				CollectionOrdering.Matching
			);
		var parts = bar.FindComponents<StackedBarPart>();
		await Assert.That((parts[0].Instance.Color, parts[0].Instance.Pattern)).IsEqualTo(("#F5C518", BarPattern.Solid));
		await Assert.That(parts[^1].Instance.Pattern).IsEqualTo(BarPattern.Hatched);
	}

	[Test]
	public async Task Render_WithTitlesStoredTwice_AsksToReviewThoseToDecide()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())) with { StoredTwice = new StoredTwiceCount(2, 1) });

		var cut = RenderLoaded();

		await Assert
			.That(cut.Find("[data-testid=housekeeping-stored-twice]").TextContent)
			.Contains("2 to decide")
			.And.Contains("2 pairs that may be one title")
			.And.Contains("1 pair is kept apart.");
	}

	[Test]
	public async Task Render_WithOnlyPairsKeptApart_SaysThereIsNothingToDecide()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())) with { StoredTwice = new StoredTwiceCount(0, 2) });

		var cut = RenderLoaded();

		await Assert
			.That(cut.Find("[data-testid=housekeeping-stored-twice]").TextContent)
			.Contains("0 to decide")
			.And.Contains("Nothing to decide")
			.And.Contains("2 pairs are kept apart.");
	}

	[Test]
	public async Task Render_BeforeAnythingRan_ReadsNeverRunAndLeavesTheStoredTwiceRowOut()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));

		var cut = RenderLoaded();

		await Assert.That(Texts(cut, "housekeeping-when")).IsEquivalentTo(["never run", "never run", "never run"]);
		await Assert.That(Texts(cut, "housekeeping-did")).IsEquivalentTo([HousekeepingView.Nothing, HousekeepingView.Nothing, HousekeepingView.Nothing]);
		await Assert.That(cut.FindAll("[data-testid=housekeeping-stored-twice]")).IsEmpty();
	}

	[Test]
	public async Task Render_WhileThePassRuns_SaysHowFarItGotAndDisablesOnlyItsRunNow()
	{
		var running = Report([new StepCount(HousekeepingStep.RunHistory, 500), new StepCount(HousekeepingStep.UsageStats, 10)]) with { FinishedAt = new None() };
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(Some.Of(running)) with { Running = Some.Of(new TaskRun(EarlierToday, true)) }));

		var cut = RenderLoaded();

		await Assert.That(Row(cut, "pass").TextContent).Contains("running since 03:00").And.Contains("2 of 8 steps done");
		await Assert.That(RunNow(cut).Select(button => button.HasAttribute("disabled"))).IsEquivalentTo([true, false, false], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Render_AfterAPassThatRemovedRows_ShowsTheTotal()
	{
		_tasks
			.OverviewAsync(Any())
			.Returns(Overview(Pass(Some.Of(Report([new StepCount(HousekeepingStep.RunHistory, 500), new StepCount(HousekeepingStep.CachedDetails, 343)])))));

		var cut = RenderLoaded();

		await Assert.That(Row(cut, "pass").TextContent).Contains("today 03:00 · ok").And.Contains("843 rows removed");
	}

	[Test]
	public async Task Render_AfterAPassWithNothingToRemove_SaysSo()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(Some.Of(Report([new StepCount(HousekeepingStep.RunHistory, 0)])))));

		var cut = RenderLoaded();

		await Assert.That(Row(cut, "pass").TextContent).Contains("nothing to remove");
	}

	[Test]
	public async Task Render_AfterAPassThatFailed_SaysWhereItStoppedAndWhy()
	{
		var failed = Report([new StepCount(HousekeepingStep.RunHistory, 500)]) with
		{
			Failure = Some.Of(new HousekeepingFailure(Some.Of(HousekeepingStep.CachedDetails), "database is locked")),
		};
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(Some.Of(failed))));

		var cut = RenderLoaded();

		await Assert.That(Row(cut, "pass").TextContent).Contains("today 03:00 · failed").And.Contains("500 rows removed");
		await Assert.That(cut.Find("[data-testid=housekeeping-error]").TextContent).IsEqualTo("Stopped at cached details: database is locked");
	}

	[Test]
	public async Task Render_AfterADatasetCheckFailed_KeepsTheLastLoadAndSaysWhy()
	{
		var loaded = new DatasetLoad(DatasetNames.ImdbRatings, new DateTimeOffset(new DateTime(2026, 10, 8, 4, 30, 0, DateTimeKind.Local)), new None(), 1_718_755);
		var failed = new DatasetCheck(DatasetNames.ImdbRatings, EarlierToday, EarlierToday, DatasetOutcome.Failed, Some.Of("datasets.imdbws.com answered 503."));
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None()), Imdb(Some.Of(failed), Some.Of(loaded))));

		var cut = RenderLoaded();

		await Assert
			.That(Row(cut, nameof(RemoteService.ImdbDatasets)).TextContent)
			.Contains("kept the ratings of Oct 8")
			.And.Contains("datasets.imdbws.com answered 503.");
	}

	[Test]
	public async Task RunNow_ForADataset_StartsThatDatasetsLoad()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));
		_tasks.LoadDatasetAsync(Any(), Any()).Returns(true);
		var cut = RenderLoaded();

		await RunNow(cut)[1].ClickAsync(new MouseEventArgs());

		_tasks.LoadDatasetAsync(RemoteService.ImdbDatasets, Any()).WasCalled(Times.Once);
		_tasks.RunPassAsync(Any()).WasNeverCalled();
	}

	[Test]
	public async Task RunNow_WhileTheTaskAlreadyRuns_SaysSo()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));
		_tasks.RunPassAsync(Any()).Returns(false);
		var cut = RenderLoaded();

		await RunNow(cut)[0].ClickAsync(new MouseEventArgs());

		await Assert.That(Toasts.Single().Description).IsEqualTo("It is already running.");
	}

	[Test]
	public async Task Changed_WhenATaskStarts_ReadsTheRowsAgainWithoutAReload()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));
		var cut = RenderLoaded();
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None()) with { Running = Some.Of(new TaskRun(EarlierToday, true)) }));

		_tasks.RaiseChanged();

		cut.WaitForState(() => Row(cut, "pass").TextContent.Contains("running since 03:00"));
		await Assert.That(RunNow(cut)[0].HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task ClearCachedDetails_OnceConfirmed_ClearsThemAndSaysSo()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));
		var cut = RenderWithDialogs();
		cut.WaitForState(() => cut.FindAll("[data-testid=housekeeping-clear-details] button").Count == 1);

		await cut.Find("[data-testid=housekeeping-clear-details] button").ClickAsync(new MouseEventArgs());
		cut.WaitForState(() => Buttons(cut, "Clear cached details").Count == 1);
		await Buttons(cut, "Clear cached details").Single().ClickAsync(new MouseEventArgs());

		cut.WaitForAssertion(() => _purger.PurgeDetailsAsync(Any()).WasCalled(Times.Once));
		_purger.PurgeAsync(Any()).WasNeverCalled();
		await Assert.That(Toasts.Single().Description).IsEqualTo("Cached details cleared.");
	}

	[Test]
	public async Task ClearCachedDetails_WhenAsked_SaysOnlyWhatItCosts()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));
		var cut = RenderWithDialogs();
		cut.WaitForState(() => cut.FindAll("[data-testid=housekeeping-clear-details] button").Count == 1);

		await cut.Find("[data-testid=housekeeping-clear-details] button").ClickAsync(new MouseEventArgs());

		cut.WaitForState(() => cut.FindAll("[role=alertdialog]").Count == 1);
		await Assert
			.That(cut.Find("[role=alertdialog]").TextContent)
			.Contains("Each of the 2,132 titles fetches its posters and ratings again the next time it is shown")
			.And.Contains("up to 2,132 provider calls")
			.And.DoesNotContain("nightly");
	}

	[Test]
	public async Task ClearShowParts_OnceConfirmed_ClearsThemAndSaysSo()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));
		var cut = RenderWithDialogs();
		cut.WaitForState(() => cut.FindAll("[data-testid=housekeeping-clear-show-parts] button").Count == 1);

		await cut.Find("[data-testid=housekeeping-clear-show-parts] button").ClickAsync(new MouseEventArgs());
		cut.WaitForState(() => Buttons(cut, "Clear show parts").Count == 1);
		await Buttons(cut, "Clear show parts").Single().ClickAsync(new MouseEventArgs());

		cut.WaitForAssertion(() => _purger.PurgeShowPartsAsync(Any()).WasCalled(Times.Once));
		_purger.PurgeAsync(Any()).WasNeverCalled();
		_purger.PurgeDetailsAsync(Any()).WasNeverCalled();
		await Assert.That(Toasts.Single().Description).IsEqualTo("Show parts cleared.");
	}

	[Test]
	public async Task Render_TheCaches_RunFromTheCheapestClearToTheCostliest()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));

		var cut = RenderLoaded();

		await Assert
			.That(cut.FindAll("[data-testid^=housekeeping-clear-]").Select(row => row.GetAttribute("data-testid") ?? string.Empty))
			.IsEquivalentTo(
				["housekeeping-clear-unmatched", "housekeeping-clear-show-parts", "housekeeping-clear-details", "housekeeping-clear-ids"],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Render_EveryCache_OffersThePlainClear()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));

		var cut = RenderLoaded();

		await Assert
			.That(cut.FindAll("[data-testid^=housekeeping-clear-] button").Select(button => button.TextContent.Trim()))
			.IsEquivalentTo(["Clear", "Clear", "Clear", "Clear"]);
	}

	[Test]
	public async Task Render_TheCaches_NameTheirColumnsInTheBand()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));

		var cut = RenderLoaded();

		await Assert
			.That(cut.Find("[data-testid=housekeeping-caches-band]").Children.Select(cell => cell.TextContent))
			.IsEquivalentTo(["Caches", "Holds", "Size", "Next trim", ""], CollectionOrdering.Matching);
	}

	[Test]
	[Arguments("details", "2,132 titles", "3.3 MB", "tomorrow 03:00", "details nobody opened in 180 days")]
	[Arguments("ids", "2,153 titles", "5.8 MB", "tomorrow 03:00", "ids nobody used in 365 days")]
	[Arguments("show-parts", "2,605 parts", "483.3 KB", "tomorrow 03:00", "seasons with no show found, after 3 days")]
	[Arguments("unmatched", HousekeepingView.Nothing, "in memory", "within the hour", "each an hour after it failed")]
	public async Task Render_ACache_SaysWhatItHoldsItsSizeAndWhenItIsNextTrimmed(string cache, string holds, string size, string next, string removes)
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(new PassTask(Some.Of(EarlierToday.AddDays(1)), new None(), new None())));

		var cut = RenderLoaded();

		await Assert.That(CacheCells(cut, cache)).IsEquivalentTo([holds, size, next, removes], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Render_TheInfoBesideACache_SaysWhatItIsForAndWhenToClearIt()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));
		var cut = RenderWithDialogs();
		cut.WaitForState(() => cut.FindAll("[data-testid=housekeeping-clear-details] [data-testid=housekeeping-cache-why]").Count == 1);

		await cut.Find("[data-testid=housekeeping-clear-details] [data-testid=housekeeping-cache-why]").TriggerEventAsync("onmouseenter", new MouseEventArgs());

		cut.WaitForState(() => cut.FindAll("[data-portal-content][data-state=open] [role=tooltip]").Count == 1);
		await Assert.That(cut.Find("[data-portal-content][data-state=open] [role=tooltip]").TextContent).Contains("Clear it when artwork or ratings look wrong.");
	}

	[Test]
	public async Task Render_TheInfoBesideACache_IsNamedForScreenReaders()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));

		var cut = RenderLoaded();

		await Assert
			.That(cut.Find("[data-testid=housekeeping-clear-details] [data-testid=housekeeping-cache-why]").GetAttribute("aria-label"))
			.IsEqualTo("About cached details");
	}

	[Test]
	public async Task ClearUnmatchedLookups_WhenClicked_ClearsThemAtOnceAndSaysSo()
	{
		_tasks.OverviewAsync(Any()).Returns(Overview(Pass(new None())));
		var cut = RenderLoaded();

		await cut.Find("[data-testid=housekeeping-clear-unmatched] button").ClickAsync(new MouseEventArgs());

		_unmatched.Clear().WasCalled(Times.Once);
		await Assert.That(Toasts).HasSingleItem();
	}

	private IReadOnlyList<ToastData> Toasts => Services.GetRequiredService<ToastService>().Toasts;

	private IRenderedComponent<HousekeepingSection> RenderLoaded()
	{
		var cut = Render<HousekeepingSection>();
		cut.WaitForState(() => cut.FindAll("[data-testid=housekeeping-task]").Count == 3);
		return cut;
	}

	private IRenderedComponent<IComponent> RenderWithDialogs()
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<HousekeepingSection>(1);
			builder.CloseComponent();
		});
	}

	private static List<IElement> Buttons(IRenderedComponent<IComponent> cut, string label)
	{
		return [.. cut.FindAll("button").Where(button => button.TextContent.Trim() == label)];
	}

	private static IElement Row(IRenderedComponent<HousekeepingSection> cut, string task)
	{
		return cut.FindAll("[data-testid=housekeeping-task]").Single(row => row.GetAttribute("data-task") == task);
	}

	private static IEnumerable<string> CacheCells(IRenderedComponent<HousekeepingSection> cut, string cache)
	{
		string[] cells = ["holds", "size", "next", "removes"];
		return cells.Select(cell => cut.Find($"[data-testid=housekeeping-clear-{cache}] [data-testid=housekeeping-cache-{cell}]").TextContent);
	}

	private static IReadOnlyList<IElement> RunNow(IRenderedComponent<HousekeepingSection> cut) => cut.FindAll("[data-testid=housekeeping-run]");

	private static IEnumerable<string> Texts(IRenderedComponent<HousekeepingSection> cut, string testId)
	{
		return cut.FindAll($"[data-testid={testId}]").Select(element => element.TextContent.Trim());
	}

	private static HousekeepingReport Report(IReadOnlyList<StepCount> steps) =>
		new(EarlierToday, Some.Of(EarlierToday.AddMinutes(2)), steps, new None(), new None());

	private static PassTask Pass(Optional<HousekeepingReport> last) => new(new None(), new None(), last);

	private static DatasetTask Imdb(Optional<DatasetCheck> last, Optional<DatasetLoad> loaded)
	{
		return new DatasetTask(RemoteService.ImdbDatasets, Some.Of(EarlierToday.AddHours(25.5)), new None(), last, loaded);
	}

	private static HousekeepingOverview Overview(PassTask pass) => Overview(pass, Imdb(new None(), new None()));

	private static HousekeepingOverview Overview(PassTask pass, DatasetTask imdb)
	{
		var anime = new DatasetTask(RemoteService.AnimeLists, Some.Of(EarlierToday.AddHours(25.5)), new None(), new None(), new None());
		return new HousekeepingOverview(
			new DatabaseSize(34_300_000, 32_100_000),
			new DatabaseParts(22_216_704, 2_719_744, 110_592),
			pass,
			[imdb, anime],
			new CachedTitles(2_132, 3_475_160, 2_153, 2_605_130, 2_605, 494_950),
			new StoredTwiceCount(0, 0)
		);
	}
}
