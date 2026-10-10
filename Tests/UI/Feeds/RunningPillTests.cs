using BlazorBlueprint.Components;
using Bunit;
using Domain.Feed.Trace;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class RunningPillTests : BunitContext
{
	private readonly Mock<ILiveRun> _run = ILiveRun.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
		_run.Tally.Getter.Returns(RunTally.Of(FateTally.None with { Started = 820, Matched = 800, NotFound = 12 }, new None(), 9000, new None(), Some.Of(2000)));
	}

	[Test]
	public async Task Render_BeforeTheSourceEnds_ShowsTheEstimatedTotal()
	{
		var cut = Render<RunningPill>(parameters => parameters.Add(pill => pill.Run, _run.Object).Add(pill => pill.Bar, true));

		await Assert.That(Text(cut)).IsEqualTo("Running · 812 / ~2,000");
		await Assert.That(cut.FindAll("[data-testid=running-bar] i")).Count().IsEqualTo(7);
	}

	[Test]
	public async Task Batch_WithNewCounts_RedrawsThePill()
	{
		var cut = Render<RunningPill>(parameters => parameters.Add(pill => pill.Run, _run.Object));

		_run.Tally.Getter.Returns(RunTally.Of(FateTally.None with { Started = 950, Matched = 900 }, Some.Of(2004), 12000, new None(), Some.Of(2000)));
		_run.RaiseBatch(new WireBatch([], [], false));

		cut.WaitForState(() => Text(cut) == "Running · 900 / 2,004");
		await Assert.That(cut.FindAll("[data-testid=running-bar]")).IsEmpty();
	}

	[Test]
	public async Task Render_AfterTheRunEnds_SaysDone()
	{
		_run.Tally.Getter.Returns(
			RunTally.Of(FateTally.None with { Started = 3, Matched = 3 }, Some.Of(3), 900, Some.Of(new FeedRunEnd(TraceEnding.Completed, 900)), new None())
		);

		var cut = Render<RunningPill>(parameters => parameters.Add(pill => pill.Run, _run.Object));

		await Assert.That(Text(cut)).IsEqualTo("Done · 3 / 3");
	}

	[Test]
	public async Task Render_AfterAFailedRun_SaysFailed()
	{
		_run.Tally.Getter.Returns(RunTally.Of(FateTally.None, new None(), 400, Some.Of(new FeedRunEnd(TraceEnding.Failed, 400)), Some.Of(120)));

		var cut = Render<RunningPill>(parameters => parameters.Add(pill => pill.Run, _run.Object));

		await Assert.That(Text(cut)).IsEqualTo("Failed · 0 / 0");
		await Assert.That(cut.Find("[data-testid=running-pill]").ClassList).Contains("is-failed");
	}

	[Test]
	public async Task Click_OpensTheLiveView()
	{
		var opened = 0;
		var cut = Render<RunningPill>(parameters => parameters.Add(pill => pill.Run, _run.Object).Add(pill => pill.OnOpen, () => opened++));

		await cut.Find("[data-testid=running-pill]").ClickAsync(new());

		await Assert.That(opened).IsEqualTo(1);
	}

	private static string Text(IRenderedComponent<RunningPill> cut)
	{
		var words = cut.Find("[data-testid=running-pill]").TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		return string.Join(' ', words);
	}
}
