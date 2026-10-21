using BlazorBlueprint.Components;
using Bunit;
using Domain.Processing;
using UI.Features.Feeds.Pages.Editor.Preview;

namespace UI.Tests.Feeds.Preview;

public class SlotLineDetailTests : BunitContext
{
	[Before(Test)]
	public void Setup() => Services.AddBlazorBlueprintComponents();

	[Test]
	public async Task Render_At86thForTwenty_ShowsTheLastInTheCutAnd65More()
	{
		var cut = RenderLine(new SlotRank(86, 20));

		await Assert.That(cut.Find(".slot-line-cut").TextContent.Trim()).IsEqualTo("limit 20");
		await Assert.That(cut.Find(".slot-line-gap").TextContent).IsEqualTo("65 more");
		await Assert.That(cut.FindAll(".slot-line-rank").Select(rank => rank.TextContent)).IsEquivalentTo(["20", "⋮", "86"]);
	}

	[Test]
	public async Task Render_ForTheOnlySlot_ShowsTheLastInWithoutAGap()
	{
		var cut = RenderLine(new SlotRank(2, 1));

		await Assert.That(cut.FindAll(".slot-line-gap")).IsEmpty();
		await Assert.That(cut.Find(".slot-line-note").TextContent).IsEqualTo("took the last slot");
	}

	private IRenderedComponent<SlotLineDetail> RenderLine(SlotRank rank)
	{
		return Render<SlotLineDetail>(parameters =>
			parameters.Add(line => line.Rank, rank).Add(line => line.LastIn, "Firefly Wedding").Add(line => line.Title, "Witch Hat Atelier")
		);
	}
}
