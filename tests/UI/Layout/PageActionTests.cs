using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Layout;

namespace UI.Tests.Layout;

public class PageActionTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WithADesktopLabel_ShowsItOnTheButton()
	{
		var cut = Render(parameters => parameters.Add(action => action.DesktopLabel, "Bulk edit"));

		await Assert.That(cut.Find("button").TextContent.Trim()).IsEqualTo("Bulk edit");
	}

	[Test]
	public async Task Render_WithoutADesktopLabel_ShowsTheLabel()
	{
		var cut = Render(_ => { });

		await Assert.That(cut.Find("button").TextContent.Trim()).IsEqualTo("Select");
	}

	[Test]
	public async Task Render_WhenActiveWithABadge_TintsTheButtonAndCounts()
	{
		var cut = Render(parameters => parameters.Add(action => action.Active, true).Add(action => action.Badge, 2));

		await Assert.That(cut.Find("button").ClassList).Contains("bg-accent");
		await Assert.That(cut.Find(".action-badge").TextContent).IsEqualTo("2");
	}

	[Test]
	public async Task Render_WithoutABadge_ShowsNoCount()
	{
		var cut = Render(_ => { });

		await Assert.That(cut.FindAll(".action-badge")).IsEmpty();
	}

	[Test]
	public async Task Render_WithDesktopMarkup_RendersItInsteadOfTheButton()
	{
		RenderFragment desktop = builder => builder.AddMarkupContent(0, "<span class=\"own\">Sync</span>");

		var cut = Render(parameters => parameters.Add(action => action.Desktop, desktop));

		await Assert.That(cut.FindAll("button")).IsEmpty();
		await Assert.That(cut.Find(".own").TextContent).IsEqualTo("Sync");
	}

	[Test]
	public async Task Render_WhenNotVisible_RendersNothing()
	{
		var cut = Render(parameters => parameters.Add(action => action.Visible, false));

		await Assert.That(cut.Markup.Trim()).IsEmpty();
	}

	[Test]
	public async Task Click_WhenPressed_InvokesOnClick()
	{
		var clicked = false;
		var cut = Render(parameters => parameters.Add(action => action.OnClick, () => clicked = true));

		await cut.Find("button").ClickAsync(new());

		await Assert.That(clicked).IsTrue();
	}

	[Test]
	public async Task Header_ForAnActionWithAToolbarSlot_HidesItselfOnPhones()
	{
		var cut = Render(parameters => parameters.Add(action => action.OnClick, () => { }));

		await Assert.That(cut.Find("button").ParentElement!.ClassList).Contains("max-md:hidden");
	}

	[Test]
	public async Task Header_ForDesktopMarkupWithoutAToolbarSlot_StaysOnPhones()
	{
		RenderFragment desktop = builder => builder.AddMarkupContent(0, "<span class=\"own\">Sync</span>");

		var cut = Render(parameters => parameters.Add(action => action.Desktop, desktop));

		await Assert.That(cut.Find(".own").ParentElement!.ClassList).DoesNotContain("max-md:hidden");
	}

	[Test]
	public async Task Toolbar_ShowsTheShortLabelAndTheBadge()
	{
		var cut = RenderInToolbar(parameters =>
			parameters.Add(action => action.DesktopLabel, "Bulk edit").Add(action => action.Badge, 2).Add(action => action.OnClick, () => { })
		);

		await Assert.That(cut.Find(".toolbar-slot-label").TextContent).IsEqualTo("Select");
		await Assert.That(cut.Find(".toolbar-badge").TextContent).IsEqualTo("2");
		await Assert.That(cut.Find(".toolbar-slot").GetAttribute("aria-label")).IsEqualTo("Select, 2");
	}

	[Test]
	public async Task Toolbar_WhenTapped_InvokesOnClickAndMarksAnActiveSlot()
	{
		var clicked = false;
		var cut = RenderInToolbar(parameters => parameters.Add(action => action.Active, true).Add(action => action.OnClick, () => clicked = true));

		await cut.Find(".toolbar-slot").ClickAsync(new());

		await Assert.That(clicked).IsTrue();
		await Assert.That(cut.Find(".toolbar-slot").ClassList).Contains("is-on");
	}

	[Test]
	public async Task Toolbar_WithAPhoneHandler_UsesItInsteadOfOnClick()
	{
		var tapped = string.Empty;
		var cut = RenderInToolbar(parameters =>
			parameters.Add(action => action.OnClick, () => tapped = "desktop").Add(action => action.PhoneOnClick, () => tapped = "phone")
		);

		await cut.Find(".toolbar-slot").ClickAsync(new());

		await Assert.That(tapped).IsEqualTo("phone");
	}

	[Test]
	public async Task Toolbar_WithOnlyAPhoneHandler_StillGetsASlotThatIsNeverTinted()
	{
		var cut = RenderInToolbar(parameters => parameters.Add(action => action.Active, true).Add(action => action.PhoneOnClick, () => { }));

		await Assert.That(cut.Find(".toolbar-slot").ClassList).DoesNotContain("is-on");
	}

	[Test]
	public async Task Toolbar_ForAnActionWithNothingToDoThere_RendersNoSlot()
	{
		RenderFragment desktop = builder => builder.AddMarkupContent(0, "<span class=\"own\">Sync</span>");

		var cut = RenderInToolbar(parameters => parameters.Add(action => action.Desktop, desktop));

		await Assert.That(cut.Markup.Trim()).IsEmpty();
	}

	private IRenderedComponent<PageAction> RenderInToolbar(Action<ComponentParameterCollectionBuilder<PageAction>> configure)
	{
		RenderTree.Add<CascadingValue<ActionPlacement>>(cascade => cascade.Add(value => value.Value, ActionPlacement.Toolbar));
		return Render(configure);
	}

	private IRenderedComponent<PageAction> Render(Action<ComponentParameterCollectionBuilder<PageAction>> configure)
	{
		return Render<PageAction>(parameters =>
		{
			parameters.Add(action => action.Icon, "square-check").Add(action => action.Label, "Select");
			configure(parameters);
		});
	}
}
