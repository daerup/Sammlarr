using BlazorBlueprint.Components;
using Bunit;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Processing;
using Domain.Providers;
using Microsoft.JSInterop;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class FeedRunLiveTests : BunitContext
{
	private static readonly Feed Documentaries = TestFeedRows.Documentaries.Overview.Feed;

	private static readonly RunLayout Layout = new RunLayout(
		[MediaProvider.Plex],
		[new(RemoteService.Tmdb, 1, false), new(RemoteService.Plex, 2, false)],
		[RemoteService.Tmdb],
		[new(new None(), RemoteService.Tmdb), new(Some.Of(RemoteService.Tmdb), RemoteService.Plex)]
	);

	private static readonly WireBatch First = new([new WireStep(13, 900, 4, (byte)WireKind.Started) { Name = "Free Solo", Year = 2018 }], [], false);

	private static readonly WireBatch Second = new([new WireStep(14, 1010, 4, (byte)WireKind.Path)], [], false);

	private static readonly Guid FirstRun = new(31, 0, 0, new byte[8]);

	private static readonly Guid SecondRun = new(32, 0, 0, new byte[8]);

	private readonly Mock<ILiveRun> _run = ILiveRun.Mock();

	private bool _hidden;

	private BunitJSModuleInterop _module = null!;

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
		_module = JSInterop.SetupModule("./js/feed-live.js");
		_run.Feed.Getter.Returns(Documentaries);
		_run.Layout.Getter.Returns(Layout);
		_run.Tally.Getter.Returns(RunTally.Before(Some.Of(400)));
		_run.Hidden.Getter.Returns(() => _hidden);
		_run.TraceKey.Getter.Returns(Some.Of(FirstRun));
		_run.SnapshotAsync(Any()).Returns(Some.Of(Snapshot(FirstRun)));
		_run.Hide().Callback(() => _hidden = true);
		_run.Show().Callback(() => _hidden = false);
	}

	[Test]
	public async Task Mount_MidRun_SendsTheSnapshotOnce()
	{
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));
		cut.WaitForState(() => _module.Invocations["mount"].Count == 1);

		_run.RaiseBatch(First);
		_run.RaiseBatch(Second);

		cut.WaitForState(() => _module.Invocations["push"].Count == 2);
		var snapshot = (WireSnapshot)_module.Invocations["mount"].Single().Arguments[2]!;
		await Assert.That(_module.Invocations["mount"].Count).IsEqualTo(1);
		await Assert.That(snapshot.CursorSeq).IsEqualTo(12);
		await Assert.That(snapshot.Titles.Select(title => title.Name)).IsEquivalentTo(["Senna"]);
	}

	[Test]
	public async Task Mount_SendsTheLayoutKeyedByService()
	{
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));

		cut.WaitForState(() => _module.Invocations["mount"].Count == 1);
		var layout = (WireLayout)_module.Invocations["mount"].Single().Arguments[1]!;
		await Assert.That(layout.Nodes.Select(node => (node.Key, node.Column))).IsEquivalentTo([("tmdb", 1), ("plex", 2)]);
		await Assert.That(layout.Edges).IsEquivalentTo([new WireEdge(null, "tmdb"), new WireEdge("tmdb", "plex")]);
		await Assert.That(layout.Details.Single().Brand.Name).IsEqualTo("TMDB");
	}

	[Test]
	public async Task Push_SendsBatchesInCursorOrder()
	{
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));
		cut.WaitForState(() => _module.Invocations["mount"].Count == 1);

		_run.RaiseBatch(First);
		_run.RaiseBatch(Second);

		cut.WaitForState(() => _module.Invocations["push"].Count == 2);
		var pushed = _module.Invocations["push"].Select(push => (WireBatch)push.Arguments[1]!).ToList();
		await Assert.That(pushed.SelectMany(batch => batch.Steps).Select(step => step.Seq)).IsEquivalentTo([13L, 14L], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Hide_StopsPushingButKeepsCounting()
	{
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));
		cut.WaitForState(() => _module.Invocations["mount"].Count == 1);

		await cut.Find("[data-testid=live-toggle]").ClickAsync(new());
		cut.WaitForState(() => _module.Invocations["dispose"].Count == 1);
		_run.Tally.Getter.Returns(RunTally.Of(FateTally.None with { Started = 40, Matched = 32 }, new None(), 4000, new None(), Some.Of(400)));
		_run.RaiseBatch(First);

		cut.WaitForState(() => cut.Find("[data-testid=live-count]").TextContent.Contains("32"));
		await Assert.That(_module.Invocations["push"]).IsEmpty();
		await Assert.That(cut.Find("[data-testid=live-body]").HasAttribute("hidden")).IsTrue();
		await Assert.That(cut.Find("[data-testid=live-count]").TextContent).IsEqualTo("32 of ~400");
	}

	[Test]
	public async Task Folded_FoldsTheView()
	{
		var folded = false;
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object).Add(live => live.OnFolded, () => folded = true));
		cut.WaitForState(() => _module.Invocations["mount"].Count == 1);

		_run.RaiseFolded();

		cut.WaitForState(() => folded);
		await Assert.That(_module.Invocations["fold"].Count).IsEqualTo(1);
	}

	[Test]
	public async Task Render_AfterTheRunFolded_FoldsAtOnce()
	{
		var folded = false;
		_run.HasFolded.Getter.Returns(true);

		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object).Add(live => live.OnFolded, () => folded = true));

		cut.WaitForState(() => folded);
		await Assert.That(folded).IsTrue();
	}

	[Test]
	public async Task Render_WhenTheModuleFailsToLoad_KeepsTheHeaderCounting()
	{
		JSInterop.TryGetInvokeHandler<IJSObjectReference>("import", "./js/feed-live.js")!.SetException(new JSException("feed-live.js did not load"));
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));

		_run.Tally.Getter.Returns(RunTally.Of(FateTally.None with { Started = 12, Matched = 9, Known = 4 }, Some.Of(380), 2500, new None(), Some.Of(400)));
		_run.RaiseBatch(First);

		cut.WaitForState(() => cut.Find("[data-testid=live-count]").TextContent == "9 of 380");
		await Assert.That(cut.Find("[data-testid=live-clock]").TextContent).IsEqualTo("0:02");
		await Assert.That(cut.Find("[data-testid=live-legend]").TextContent).Contains("Already known 4");
	}

	[Test]
	public async Task Mount_WhileItIsUnderway_PushesTheBatchesThatArrivedMeanwhile()
	{
		var mounting = _module.SetupVoid("mount", _ => true);
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));
		cut.WaitForState(() => _module.Invocations["mount"].Count == 1);

		_run.RaiseBatch(First);
		_run.RaiseBatch(Second);
		mounting.SetVoidResult();

		cut.WaitForState(() => _module.Invocations["push"].Count == 2);
		var pushed = _module.Invocations["push"].Select(push => (WireBatch)push.Arguments[1]!).ToList();
		await Assert.That(pushed.SelectMany(batch => batch.Steps).Select(step => step.Seq)).IsEquivalentTo([13L, 14L], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Show_AfterHide_MountsAFreshSnapshot()
	{
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));
		cut.WaitForState(() => _module.Invocations["mount"].Count == 1);

		await cut.Find("[data-testid=live-toggle]").ClickAsync(new());
		cut.WaitForState(() => _module.Invocations["dispose"].Count == 1);
		await cut.Find("[data-testid=live-toggle]").ClickAsync(new());

		cut.WaitForState(() => _module.Invocations["mount"].Count == 2);
		await Assert.That(cut.Find("[data-testid=live-body]").HasAttribute("hidden")).IsFalse();
		_run.SnapshotAsync(Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task Push_AfterTheRunChanges_RemountsFromAFreshSnapshot()
	{
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));
		cut.WaitForState(() => _module.Invocations["mount"].Count == 1);

		_run.TraceKey.Getter.Returns(Some.Of(SecondRun));
		_run.SnapshotAsync(Any()).Returns(Some.Of(Snapshot(SecondRun)));
		_run.RaiseBatch(First);

		cut.WaitForState(() => _module.Invocations["mount"].Count == 2);
		await Assert.That(_module.Invocations["dispose"].Count).IsEqualTo(1);
		await Assert.That(_module.Invocations["push"]).IsEmpty();
	}

	[Test]
	public async Task Footer_WhileRunning_ShowsThePageAndTheWaiting()
	{
		var progress = new RunProgress(Some.Of(new SourcePage(8, Some.Of(12))), 4);
		_run.Tally.Getter.Returns(RunTally.Before(Some.Of(400)) with { Progress = progress });

		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));

		await Assert.That(Segments(cut)).IsEquivalentTo(["Source page 8 of 12", "4 waiting on a service"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Footer_AfterTheRun_ShowsTheFeedsRules()
	{
		var counts = FeedRunCounts.None with { Total = 200, Added = 3, Filtered = 120, Skipped = 40 };
		var ended = new FeedRunEnd(TraceEnding.Completed, 9000) { Counts = Some.Of(counts) };
		_run.Tally.Getter.Returns(RunTally.Of(FateTally.None with { Started = 200, Matched = 200 }, Some.Of(200), 9000, Some.Of(ended), new None()));
		_run.Feed.Getter.Returns(Documentaries with { Limit = Some.Of(new QueryLimit { Count = 5 }) });

		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));

		await Assert
			.That(Segments(cut))
			.IsEquivalentTo(["Then the feed's rules: +3 to your wantlist", "120 filtered out", "40 past the limit of 5"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Footer_BeforeTheFirstPage_SaysItIsReadingTheList()
	{
		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));

		await Assert.That(Segments(cut)).IsEquivalentTo(["Reading the source list", "0 waiting on a service"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Footer_AfterARunThatAddedNothingWithoutALimit_SaysWhatItKept()
	{
		var counts = FeedRunCounts.None with { Total = 40, Kept = 31, Filtered = 9 };
		var ended = new FeedRunEnd(TraceEnding.Completed, 3000) { Counts = Some.Of(counts) };
		_run.Tally.Getter.Returns(RunTally.Of(FateTally.None with { Started = 40, Matched = 40 }, Some.Of(40), 3000, Some.Of(ended), new None()));

		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));

		await Assert.That(Segments(cut)).IsEquivalentTo(["Then the feed's rules: 31 kept, nothing new", "9 filtered out"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Footer_AfterAFailedRun_KeepsTheRunningLine()
	{
		var ended = new FeedRunEnd(TraceEnding.Failed, 400) { Counts = Some.Of(FeedRunCounts.None) };
		var tally = RunTally.Of(FateTally.None, new None(), 400, Some.Of(ended), new None());
		_run.Tally.Getter.Returns(tally with { Progress = new RunProgress(Some.Of(new SourcePage(1, new None())), 0) });

		var cut = Render<FeedRunLive>(parameters => parameters.Add(live => live.Run, _run.Object));

		await Assert.That(Segments(cut)).IsEquivalentTo(["Source page 1", "0 waiting on a service"], CollectionOrdering.Matching);
	}

	private static IReadOnlyList<string> Segments(IRenderedComponent<FeedRunLive> cut)
	{
		return
		[
			.. cut.FindAll("[data-testid=live-foot] .live-foot-main > span")
				.Select(span => string.Join(' ', span.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))),
		];
	}

	private static TraceSnapshot Snapshot(Guid run)
	{
		var title = new TitleInFlight(3, "Senna", Some.Of(2010), Some.Of(new HopPlace(RemoteService.Tmdb, TraceStage.Ids)), new None(), new None());
		return new TraceSnapshot(
			new TraceCursor(Some.Of(run), 12),
			850,
			[title],
			[new NodeTally(new HopPlace(RemoteService.Tmdb, TraceStage.Ids), 2, 0, 0, 0, 1, 140)],
			FateTally.None with
			{
				Started = 3,
				Matched = 2,
			},
			new None(),
			new None()
		);
	}
}
