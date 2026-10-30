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

	private IRenderedComponent<PageAction> Render(Action<ComponentParameterCollectionBuilder<PageAction>> configure)
	{
		return Render<PageAction>(parameters =>
		{
			parameters.Add(action => action.Icon, "square-check").Add(action => action.Label, "Select");
			configure(parameters);
		});
	}
}
