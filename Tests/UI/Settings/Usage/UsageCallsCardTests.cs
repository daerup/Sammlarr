using BlazorBlueprint.Components;
using Bunit;
using Domain.Usage;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageCallsCardTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_TheCallsTab_ListsEveryOutcomeWithAnsweredInItsGrey()
	{
		var cut = RenderCard();

		var swatches = cut.FindAll("[data-testid=usage-outcome-legend] > span")
			.Select(item => $"{item.TextContent.Trim()}|{item.QuerySelector("span")?.GetAttribute("style")}");

		await Assert.That(swatches.First()).IsEqualTo("Answered|background: var(--usage-answered)");
		await Assert.That(swatches).Count().IsEqualTo(5);
	}

	[Test]
	public async Task Tab_SwitchedToAnswerTime_PicksAServiceAndMarksTheMedianAndP95()
	{
		var cut = RenderCard();

		await cut.FindAll("button").First(button => button.TextContent.Trim() == "Answer time").ClickAsync();

		var services = cut.FindAll("[data-testid=usage-latency-services] button").Select(button => button.TextContent.Trim());
		await Assert.That(services).IsEquivalentTo(["All but datasets", "TMDb", "Plex"]);
		await Assert.That(cut.Find("[data-testid=usage-latency-marks]").TextContent).Contains("median").And.Contains("p95");
		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("All but datasets: every attempt by how long it took");
	}

	private IRenderedComponent<UsageCallsCard> RenderCard()
	{
		return Render<UsageCallsCard>(parameters => parameters.Add(card => card.Snapshot, UsageSamples.Snapshot()).Add(card => card.Range, UsageRange.Day));
	}
}
