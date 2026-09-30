using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Layout;

namespace UI.Tests.Layout;

public class PageContainerTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Fill_KeepsVerticalPaddingInsideTheScrollArea_SoAGrownTileIsNotClipped()
	{
		var cut = Render(fill: true);

		await Assert.That(cut.Find(".overflow-y-auto").ClassList).Contains("py-2");
	}

	[Test]
	public async Task WithoutFill_LeavesTheScrollingToTheLayout()
	{
		var cut = Render(fill: false);

		await Assert.That(cut.FindAll(".overflow-y-auto")).IsEmpty();
	}

	private IRenderedComponent<PageContainer> Render(bool fill) =>
		Render<PageContainer>(parameters =>
			parameters
				.Add(container => container.Heading, "Your wantlist")
				.Add(container => container.Fill, fill)
				.Add(container => container.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>body</p>")))
		);
}
