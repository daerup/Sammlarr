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

	[Test]
	public async Task Range_CountsFromTheFirstItemOnThePageToTheLastOneThatExists()
	{
		var cut = Render(totalPages: 4, currentPage: 4, totalItems: 69);

		await Assert.That(cut.Find(".pager-range").TextContent).IsEqualTo("61–69 of 69");
	}

	private IRenderedComponent<PaginationControls> Render(int totalPages, int currentPage = 1, int totalItems = 80) =>
		Render<PaginationControls>(parameters =>
			parameters
				.Add(pager => pager.CurrentPage, currentPage)
				.Add(pager => pager.TotalPages, totalPages)
				.Add(pager => pager.TotalItems, totalItems)
				.Add(pager => pager.ItemsPerPage, 20)
		);
}
