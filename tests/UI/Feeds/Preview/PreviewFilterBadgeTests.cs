using BlazorBlueprint.Components;
using Bunit;
using UI.Features.Feeds.Pages.Editor.Preview;

namespace UI.Tests.Feeds.Preview;

public class PreviewFilterBadgeTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Flash_WhenAReplanMovedTheCount_FlashesTheBadge()
	{
		var cut = RenderBadge(count: 3, flash: 0);

		cut.Render(parameters => parameters.Add(badge => badge.Count, 5).Add(badge => badge.Flash, 1));

		await Assert.That(Badge(cut).ClassList).Contains("is-flashing");
	}

	[Test]
	public async Task Flash_WhenAReplanLeftTheCount_StaysStill()
	{
		var cut = RenderBadge(count: 3, flash: 0);

		cut.Render(parameters => parameters.Add(badge => badge.Count, 3).Add(badge => badge.Flash, 1));

		await Assert.That(Badge(cut).ClassList).DoesNotContain("is-flashing");
	}

	[Test]
	public async Task Flash_WhenTheCountGrowsWhileTheListLoads_StaysStill()
	{
		var cut = RenderBadge(count: 3, flash: 0);

		cut.Render(parameters => parameters.Add(badge => badge.Count, 9).Add(badge => badge.Flash, 0));

		await Assert.That(Badge(cut).ClassList).DoesNotContain("is-flashing");
	}

	private IRenderedComponent<PreviewFilterBadge> RenderBadge(int count, int flash)
	{
		return Render<PreviewFilterBadge>(parameters =>
			parameters.Add(badge => badge.Icon, "check").Add(badge => badge.Count, count).Add(badge => badge.Flash, flash)
		);
	}

	private static AngleSharp.Dom.IElement Badge(IRenderedComponent<PreviewFilterBadge> cut) => cut.Find("button.preview-filter-badge");
}
