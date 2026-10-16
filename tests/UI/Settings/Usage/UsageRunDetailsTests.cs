using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageRunDetailsTests : BunitContext
{
	private static readonly FeedId Feed = FeedId.From(Guid.NewGuid());

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_AScheduledRunThatHitA429_SaysWhatItChangedAndHowManyCallsWereLimited()
	{
		var cut = RenderDetails(new Scheduled("0 0 3 * * *"), 4, 1, UsageSamples.Tally(answered: 100, after: 10, gaveUp: 5));

		await Assert.That(cut.Markup).Contains("· scheduled");
		await Assert.That(cut.Find("[data-testid=usage-run-change]").TextContent).Contains("+4").And.Contains("−1");
		await Assert.That(cut.Find("[data-testid=usage-run-limited]").TextContent.Trim()).EndsWith("15 rate-limited, 10 answered");
	}

	[Test]
	public async Task Render_AManualRunWithoutChangesOrLimits_SaysNoChangeAndLeavesOutLimited()
	{
		var cut = RenderDetails(new Manual(), 0, 0, UsageSamples.Tally(answered: 100));

		await Assert.That(cut.Markup).Contains("· manual");
		await Assert.That(cut.Find("[data-testid=usage-run-change]").TextContent).Contains("no change");
		await Assert.That(cut.FindAll("[data-testid=usage-run-limited]")).IsEmpty();
	}

	private IRenderedComponent<UsageRunDetails> RenderDetails(FeedRunTrigger trigger, int added, int removed, UsageTally calls)
	{
		var run = new UsageRun(
			RunId.From(Guid.NewGuid()),
			UsageSamples.To,
			TimeSpan.FromSeconds(4),
			trigger,
			50,
			added,
			removed,
			0,
			0,
			calls,
			new Dictionary<RemoteService, UsageTally>()
		);
		var line = new UsageFeedLine(
			Feed,
			"Latest shows",
			FeedProvider.Mdblist,
			true,
			Some.Of(run.StartedAt),
			[run],
			new UsageFeedStats(0, 0, 0, 0, 0, 0, TimeSpan.Zero),
			new Dictionary<RemoteService, UsageFeedStats>()
		);
		return Render<UsageRunDetails>(parameters => parameters.Add(details => details.Line, line).Add(details => details.Run, run));
	}
}
