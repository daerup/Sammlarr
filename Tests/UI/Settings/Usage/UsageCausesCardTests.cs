using BlazorBlueprint.Components;
using Bunit;
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
	public async Task Render_AllCauses_ColoursTheDonutFromAnsweredFirst()
	{
		var cut = RenderCard();

		await Assert.That(cut.Find("[data-testid=usage-donut]").GetAttribute("style")).StartsWith("--chart-1: var(--usage-answered)");
		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("All causes").And.Contains("140 calls");
	}

	[Test]
	public async Task Cause_Picked_RedrawsTheDonutForIt()
	{
		var cut = RenderCard();

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Search")).ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("Search").And.Contains("14 calls");
		await Assert
			.That(cut.Find("[data-testid=usage-donut]").GetAttribute("style"))
			.IsEqualTo("--chart-1: var(--usage-answered); --chart-2: var(--usage-cancelled)");
	}

	[Test]
	public async Task Outcome_AnsweredStruckOut_LeavesTheFourKindsOfTrouble()
	{
		var cut = RenderCard();

		await cut.FindAll("[data-testid=usage-outcome]").First(row => row.TextContent.Contains("Answered")).ClickAsync();

		await Assert
			.That(cut.Find("[data-testid=usage-donut]").GetAttribute("style"))
			.IsEqualTo("--chart-1: var(--usage-after-429); --chart-2: var(--usage-gave-up); --chart-3: var(--usage-failed); --chart-4: var(--usage-cancelled)");
		await Assert.That(cut.FindAll("[data-testid=usage-outcome]").First().GetAttribute("aria-pressed")).IsEqualTo("false");
	}

	[Test]
	public async Task Cause_PickedButAbsentFromTheNextWindow_FallsBackToAllCauses()
	{
		var cut = RenderCard();
		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Search")).ClickAsync();

		cut.Render(parameters => parameters.Add(card => card.Period, UsageSamples.Snapshot().Previous));

		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("All causes");
	}

	private IRenderedComponent<UsageCausesCard> RenderCard()
	{
		return Render<UsageCausesCard>(parameters => parameters.Add(card => card.Period, UsageSamples.Snapshot().Current));
	}
}
