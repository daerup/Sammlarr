using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components.Runs;

namespace UI.Tests.Feeds.Runs;

public class RunCallsTests : BunitContext
{
	private static readonly FeedRun Run = TestFeeds.Run(TestFeeds.Feed(Guid.NewGuid()), Guid.NewGuid()) with
	{
		Duration = TimeSpan.FromSeconds(21.4),
		Counts = FeedRunCounts.None with { Total = 300 },
		Calls =
		[
			new FeedRunCalls(RemoteService.Tmdb, UsageOutcome.Answered, 290),
			new FeedRunCalls(RemoteService.Plex, UsageOutcome.Answered, 520),
			new FeedRunCalls(RemoteService.Plex, UsageOutcome.AnsweredAfter429, 37),
			new FeedRunCalls(RemoteService.Plex, UsageOutcome.GaveUpOn429, 4),
		],
		Times = [new FeedRunTimes(RemoteService.Plex, 250, 600), new FeedRunTimes(RemoteService.Tmdb, 100, 290)],
	};

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ARunsCallCount_ReadsAsPlainText()
	{
		var cut = Render<Harness>();

		await Assert.That(cut.Find("[data-testid=run-calls]").TextContent.Trim()).IsEqualTo("851 calls");
	}

	[Test]
	public async Task Count_Clicked_OpensTheFiguresAndEveryServiceBusiestFirst()
	{
		var cut = Render<Harness>();

		await cut.Find("[data-testid=run-calls]").ClickAsync();

		var panel = cut.WaitForElement("[data-portal-content] [data-testid=run-calls-panel]");
		await Assert
			.That(panel.QuerySelector("[data-testid=run-calls-figures]")!.TextContent)
			.Contains("851")
			.And.Contains("2.8")
			.And.Contains("95.2%")
			.And.Contains("21 s 400 ms");
		await Assert
			.That(panel.QuerySelectorAll("[data-testid=run-calls-service]").Select(row => row.GetAttribute("data-service")!))
			.IsEquivalentTo(["Plex", "Tmdb"], CollectionOrdering.Matching);
		await Assert.That(panel.QuerySelectorAll("[data-testid=usage-strip]")).Count().IsEqualTo(2);
		await Assert.That(panel.QuerySelector("[data-testid=run-calls-typical]")!.TextContent).Contains("1.8× this feed's typical run");
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			Optional<RunTypical> typical = Some.Of(new RunTypical(470, 9));
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<RunCalls>(1);
			builder.AddComponentParameter(2, nameof(RunCalls.Run), Run);
			builder.AddComponentParameter(3, nameof(RunCalls.Typical), typical);
			builder.CloseComponent();
		}
	}
}
