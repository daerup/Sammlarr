using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageCausesCardTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_AllCauses_PutsTheShareAnsweredFirstTryInTheHalfCircle()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await Assert.That(cut.Find("[data-testid=usage-gauge-centre]").TextContent).IsEqualTo("92.9%");
		await Assert.That(cut.FindAll("[data-testid=usage-gauge-segment]")).Count().IsEqualTo(5);
		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("All causes").And.Contains("140 calls");
	}

	[Test]
	public async Task Cause_Picked_RedrawsTheHalfCircleForIt()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Search")).ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("Search").And.Contains("14 calls");
		await Assert
			.That(cut.FindAll("[data-testid=usage-gauge-segment]").Select(segment => segment.GetAttribute("data-outcome")!))
			.IsEquivalentTo(["Answered", "Cancelled"]);
		await Assert.That(cut.Find("[data-testid=usage-gauge-centre]").TextContent).IsEqualTo("71.4%");
	}

	[Test]
	public async Task Outcome_WithoutCalls_IsGreyedAndHasNoShare()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Search")).ClickAsync();

		var failed = cut.FindAll("[data-testid=usage-outcome]").Single(row => row.TextContent.Contains("Failed"));
		await Assert.That(failed.GetAttribute("data-empty")).IsEqualTo("true");
		await Assert.That(failed.TextContent).DoesNotContain("%");
	}

	[Test]
	public async Task Cause_WithoutTrouble_SaysEveryCallWasAnswered()
	{
		var cut = RenderCard(Period(new UsageCauseLine(UsageCause.Browse, UsageSamples.Tally(answered: 75))));

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Browse")).ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-no-trouble]").TextContent).Contains("No trouble:").And.Contains("all 75 calls were answered.");
		await Assert.That(cut.FindAll("[data-testid=usage-outcome]")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Render_UnmarkedAndConnectionTestCalls_NamesThemWithAHint()
	{
		var cut = RenderCard(
			Period(
				new UsageCauseLine(UsageCause.Other, UsageSamples.Tally(answered: 25, cancelled: 2)),
				new UsageCauseLine(UsageCause.KeyTest, UsageSamples.Tally(answered: 1))
			)
		);

		await Assert.That(cut.FindAll("[data-testid=usage-cause-hinted]").Select(name => name.TextContent)).IsEquivalentTo(["Untagged", "Connection tests"]);
	}

	[Test]
	public async Task Cause_PickedButAbsentFromTheNextWindow_FallsBackToAllCauses()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);
		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Search")).ClickAsync();

		cut.Render(parameters => parameters.Add(card => card.Period, UsageSamples.Snapshot().Previous));

		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("All causes");
	}

	private static UsagePeriod Period(params UsageCauseLine[] causes)
	{
		return UsageSamples.Period([UsageSamples.Line(RemoteService.Tmdb, UsageTally.Sum(causes.Select(line => line.Tally)))], causes);
	}

	private IRenderedComponent<UsageCausesCard> RenderCard(UsagePeriod period)
	{
		return Render<UsageCausesCard>(parameters => parameters.Add(card => card.Period, period));
	}
}
