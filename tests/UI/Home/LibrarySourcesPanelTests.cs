using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using TUnit.Assertions.Enums;
using UI.Features.Home.Components;
using UI.Shared.Bars;

namespace UI.Tests.Home;

public class LibrarySourcesPanelTests : BunitContext
{
	private static readonly IReadOnlyList<SourceShare> Sources = [new("hdlists/comedy-movies-2001-20207", 1), new("Added by hand or from Plex", 203)];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ForEachSource_DrawsAPartOfItsCount()
	{
		var cut = Render<Harness>();

		await Assert
			.That(cut.FindComponents<StackedBarPart>().Select(part => (part.Instance.Name, part.Instance.Value)))
			.IsEquivalentTo([("hdlists/comedy-movies-2001-20207", 1L), ("Added by hand or from Plex", 203L)], CollectionOrdering.Matching);
		await Assert.That(cut.FindComponent<StackedBar>().Instance.ShowLegend).IsTrue();
	}

	[Test]
	public async Task Legend_WhenASourceIsHidden_CountsOnlyWhatIsLeft()
	{
		var cut = Render<Harness>();

		await cut.FindAll(".sb-key")[1].ClickAsync(new());

		await Assert.That(cut.Find(".band-note").TextContent).IsEqualTo($"{1:N0} of {204:N0} titles");
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<LibrarySourcesPanel>(1);
			builder.AddComponentParameter(2, nameof(LibrarySourcesPanel.Sources), Sources);
			builder.AddComponentParameter(3, nameof(LibrarySourcesPanel.Total), 204);
			builder.CloseComponent();
		}
	}
}
