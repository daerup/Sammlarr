using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class HelpTipTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ByDefault_ShowsTheEditorsInfoIcon()
	{
		var cut = Render<HelpTip>(parameters => parameters.Add(tip => tip.Title, "Enabled").AddChildContent("Toggle to enable or disable this feed."));

		await Assert.That(cut.FindComponent<BbTooltip>().Instance.DelayDuration).IsEqualTo(300);
		await Assert.That(cut.FindComponent<BbTooltipTrigger>().Instance.Class).IsEqualTo("ml-1 inline-flex items-center p-0 text-muted-foreground cursor-default");
		await Assert.That(cut.Find("svg").GetAttribute("width")).IsEqualTo("14");
	}

	[Test]
	public async Task Render_WithItsOwnLook_PassesItToTheTooltip()
	{
		var cut = Render<HelpTip>(parameters =>
			parameters
				.Add(tip => tip.Title, "Answer time")
				.Add(tip => tip.Delay, 150)
				.Add(tip => tip.Placement, PopoverPlacement.BottomEnd)
				.Add(tip => tip.IconSize, 12)
				.Add(tip => tip.TriggerClass, "inline-flex text-muted-foreground")
				.Add(tip => tip.Focusable, false)
				.Add(tip => tip.ContentClass, "w-[300px]")
				.Add(tip => tip.ContentTestId, "usage-answer-time-help")
				.AddUnmatched("aria-label", "About answer time")
				.AddChildContent("Each square is a range of answer times.")
		);

		var tooltip = cut.FindComponent<BbTooltip>().Instance;
		var trigger = cut.FindComponent<BbTooltipTrigger>().Instance;
		var content = cut.FindComponent<BbTooltipContent>().Instance;
		await Assert.That(tooltip.DelayDuration).IsEqualTo(150);
		await Assert.That(tooltip.Placement).IsEqualTo(PopoverPlacement.BottomEnd);
		await Assert.That(trigger.Class).IsEqualTo("inline-flex text-muted-foreground");
		await Assert.That(trigger.Focusable).IsFalse();
		await Assert.That(content.Class).IsEqualTo("w-[300px]");
		await Assert.That(content.AdditionalAttributes!["data-testid"]).IsEqualTo("usage-answer-time-help");
		await Assert.That(cut.Find("svg").GetAttribute("width")).IsEqualTo("12");
		await Assert.That(cut.FindAll("[aria-label='About answer time']")).HasSingleItem();
	}

	[Test]
	public async Task Phone_TappingTheIcon_OpensASheetWithTheTitleAndAHiddenClose()
	{
		var cut = RenderOnPhone();

		await Assert.That(cut.Component.FindComponents<BbTooltip>()).IsEmpty();
		await cut.Component.Find("button[aria-label='About Enabled']").ClickAsync();

		var sheet = cut.Portal.WaitForElement("[data-sheet][aria-label='Enabled']");
		await Assert.That(sheet.TextContent).Contains("Toggle to enable or disable this feed.");
		await Assert.That(sheet.QuerySelector("button.sr-only")!.TextContent.Trim()).IsEqualTo("Close");
	}

	[Test]
	public async Task Phone_WithTheCallersLabel_KeepsIt()
	{
		var cut = RenderOnPhone("About the answer time");

		await Assert.That(cut.Component.FindAll("button[aria-label='About the answer time']")).HasSingleItem();
	}

	private PhoneRender<HelpTip> RenderOnPhone(string? ariaLabel = null)
	{
		return this.RenderOnPhone<HelpTip>(parameters =>
		{
			parameters.Add(tip => tip.Title, "Enabled").AddChildContent("Toggle to enable or disable this feed.");
			if (ariaLabel is not null)
			{
				parameters.AddUnmatched("aria-label", ariaLabel);
			}
		});
	}
}
