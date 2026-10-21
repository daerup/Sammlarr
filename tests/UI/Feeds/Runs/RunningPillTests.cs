using System.Globalization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Feed.Trace;
using Microsoft.AspNetCore.Components.Web;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components.Runs;

namespace UI.Tests.Feeds.Runs;

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

		await Assert.That(Text(cut)).IsEqualTo($"Running · 812 / ~{2000:N0}");
		await Assert.That(cut.FindAll("[data-testid=running-bar]")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Bar_PointedAtWhileRunning_ListsWhatIsBeingLookedUpAndWhatIsStillToCome()
	{
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<RunningPill>(1);
			builder.AddComponentParameter(2, nameof(RunningPill.Run), _run.Object);
			builder.AddComponentParameter(3, nameof(RunningPill.Bar), true);
			builder.CloseComponent();
		});

		await cut.Find("[data-testid=running-bar] [data-testid=stacked-bar-track] [data-part]").TriggerEventAsync("onmouseenter", new MouseEventArgs());
		var tip = cut.WaitForElement("[data-portal-content][data-state=open] [data-testid=stacked-bar-tip]");

		await Assert.That(tip.TextContent).StartsWith($"812 of ~{2000:N0}");
		await Assert
			.That(
				tip.QuerySelectorAll("[data-testid=stacked-bar-tip-row]")
					.TakeLast(2)
					.Select(row => string.Join("|", row.Children.Select(cell => cell.TextContent.Trim())))
			)
			.IsEquivalentTo(["Being looked up|8|0.4%", $"Still to come|{1180:N0}|59.0%"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Render_InASwissGermanCulture_GroupsTheCountsLikeTheFeedCards()
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");

		var cut = Render<RunningPill>(parameters => parameters.Add(pill => pill.Run, _run.Object));

		await Assert.That(Text(cut)).IsEqualTo($"Running · 812 / ~2{CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator}000");
	}

	[Test]
	public async Task Batch_WithNewCounts_RedrawsThePill()
	{
		var cut = Render<RunningPill>(parameters => parameters.Add(pill => pill.Run, _run.Object));

		_run.Tally.Getter.Returns(RunTally.Of(FateTally.None with { Started = 950, Matched = 900 }, Some.Of(2004), 12000, new None(), Some.Of(2000)));
		_run.RaiseBatch(new WireBatch([], [], false));

		cut.WaitForState(() => Text(cut) == $"Running · 900 / {2004:N0}");
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
	}

	[Test]
	public async Task Click_OpensTheLiveView()
	{
		var opened = 0;
		var cut = Render<RunningPill>(parameters => parameters.Add(pill => pill.Run, _run.Object).Add(pill => pill.OnOpen, () => opened++));

		await cut.Find("[data-testid=running-pill]").ClickAsync(new());

		await Assert.That(opened).IsEqualTo(1);
	}

	private static string Text(IRenderedComponent<RunningPill> cut) => cut.Find("[data-testid=running-pill]").TextContent.Words();
}
