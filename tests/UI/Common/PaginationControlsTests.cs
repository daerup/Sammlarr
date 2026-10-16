using BlazorBlueprint.Components;
using Bunit;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class PaginationControlsTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WhenThereIsMoreThanOnePage_FollowsThePillWithTheDockSentinel()
	{
		var cut = Render(totalPages: 4);

		await Assert.That(cut.Find(".pager-float + .pager-end")).IsNotNull();
	}

	[Test]
	public async Task Render_WhenEverythingFitsOnOnePage_ShowsNeitherThePillNorTheSentinel()
	{
		var cut = Render(totalPages: 1);

		await Assert.That(cut.FindAll(".pager-pill")).IsEmpty();
		await Assert.That(cut.FindAll(".pager-end")).IsEmpty();
	}

	private IRenderedComponent<PaginationControls> Render(int totalPages) =>
		Render<PaginationControls>(parameters => parameters.Add(pager => pager.TotalPages, totalPages));
}
