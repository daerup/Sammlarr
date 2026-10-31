using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class DetailTipTests : BunitContext
{
	private static readonly RenderFragment Badge = builder => builder.AddMarkupContent(0, "<span class=\"badge\">Plex</span>");

	private static readonly RenderFragment Detail = builder => builder.AddMarkupContent(0, "Added from your Plex watchlist.");

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_PutsTheTriggerInTheTooltipTrigger()
	{
		var cut = Render<DetailTip>(parameters =>
			parameters.Add(tip => tip.Title, "Where it came from").Add(tip => tip.TriggerClass, "p-0").Add(tip => tip.Trigger, Badge).Add(tip => tip.Detail, Detail)
		);

		var trigger = cut.FindComponent<BbTooltipTrigger>();
		await Assert.That(trigger.Instance.Class).IsEqualTo("p-0");
		await Assert.That(trigger.Find(".badge").TextContent).IsEqualTo("Plex");
		await Assert.That(cut.FindComponent<BbTooltip>().Instance.DelayDuration).IsEqualTo(300);
	}

	[Test]
	public async Task Render_WithItsOwnTiming_PassesItAndTheAttributesOn()
	{
		var cut = Render<DetailTip>(parameters =>
			parameters
				.Add(tip => tip.Title, "Monday")
				.Add(tip => tip.Delay, 100)
				.Add(tip => tip.Focusable, false)
				.Add(tip => tip.ContentClass, "max-w-xs")
				.Add(tip => tip.Trigger, Badge)
				.Add(tip => tip.Detail, Detail)
				.AddUnmatched("data-testid", "usage-quota-day")
		);

		await Assert.That(cut.FindComponent<BbTooltip>().Instance.DelayDuration).IsEqualTo(100);
		await Assert.That(cut.FindComponent<BbTooltipTrigger>().Instance.Focusable).IsFalse();
		await Assert.That(cut.FindComponent<BbTooltipContent>().Instance.Class).IsEqualTo("max-w-xs");
		await Assert.That(cut.FindAll("[data-testid=usage-quota-day]")).HasSingleItem();
	}

	[Test]
	public async Task Phone_TappingTheTrigger_OpensTheDetailInASheet()
	{
		var cut = this.RenderOnPhone<DetailTip>(parameters =>
			parameters
				.Add(tip => tip.Title, "Where it came from")
				.Add(tip => tip.Trigger, Badge)
				.Add(tip => tip.Detail, Detail)
				.AddUnmatched("aria-label", "Plex watchlist")
		);

		await Assert.That(cut.Component.FindComponents<BbTooltip>()).IsEmpty();
		await cut.Component.Find("button[aria-label='Plex watchlist'] .badge").ClickAsync();

		var sheet = cut.Portal.WaitForElement("[data-sheet][aria-label='Where it came from']");
		await Assert.That(sheet.TextContent).Contains("Added from your Plex watchlist.");
	}
}
