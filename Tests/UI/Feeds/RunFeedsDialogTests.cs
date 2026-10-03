using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Domain.Feed;
using Microsoft.AspNetCore.Components;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class RunFeedsDialogTests : BunitContext
{
	private static readonly RunCandidate Stalled = Candidate("Anime, currently airing", RunGroup.WorthRunning);
	private static readonly RunCandidate Documentaries = Candidate("hdlists/latest-hd-documentary-movies-1980-to-today", RunGroup.ManualOnly);
	private static readonly RunCandidate WeeklyPicks = Candidate("Weekly picks", RunGroup.ManualOnly, enabled: false);
	private static readonly RunCandidate Trending = Candidate("snoak/trending-movies", RunGroup.SoonAnyway);

	private static readonly RunCandidate[] All = [Stalled, Documentaries, WeeklyPicks, Trending];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Open_WithASelection_TicksOnlyTheChosenFeedsThatCanRun()
	{
		var cut = RenderDialog(new HashSet<FeedId> { Documentaries.Id, WeeklyPicks.Id, Trending.Id });

		await Assert.That(cut.Find(".run-foot-push").TextContent).Contains("Run 1 feed");
		await Assert.That(cut.Find(".run-from").TextContent).Contains("2 are paused or already in the queue and can't run");
	}

	[Test]
	public async Task Open_WithoutASelection_TicksTheFeedsWorthRunningNow()
	{
		var cut = RenderDialog(null);

		await Assert.That(cut.Find(".run-foot-push").TextContent).Contains("Run 1 feed");
		await Assert.That(cut.FindAll(".run-from")).IsEmpty();
	}

	[Test]
	public async Task Open_HatchesOnlyThePausedFeed()
	{
		var cut = RenderDialog(null);

		var paused = cut.FindAll(".run-row.is-paused");
		await Assert.That(paused).HasSingleItem();
		await Assert.That(paused[0].TextContent).Contains(WeeklyPicks.Name);
	}

	private IRenderedComponent<IComponent> RenderDialog(IReadOnlySet<FeedId>? selection) =>
		Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<RunFeedsDialog>(1);
			builder.AddComponentParameter(2, nameof(RunFeedsDialog.Open), true);
			builder.AddComponentParameter(3, nameof(RunFeedsDialog.Candidates), All);
			builder.AddComponentParameter(4, nameof(RunFeedsDialog.Pending), new HashSet<FeedId> { Trending.Id });
			builder.AddComponentParameter(5, nameof(RunFeedsDialog.Selection), selection);
			builder.CloseComponent();
		});

	private static RunCandidate Candidate(string name, RunGroup group, bool enabled = true) =>
		new(FeedId.From(Guid.NewGuid()), name, "Manual", new LastRun(true, 0, 0), null, new None(), group, enabled);
}
