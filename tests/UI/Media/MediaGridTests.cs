using BlazorBlueprint.Components;
using Bunit;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaGridTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WhileLoading_ShowsTwelveSkeletonsInsteadOfTheTiles()
	{
		var cut = Render<MediaGrid>(parameters => parameters.Add(grid => grid.Loading, true).AddChildContent("<article>tile</article>"));

		await Assert.That(cut.Find(".media-grid").Children.Length).IsEqualTo(12);
		await Assert.That(cut.FindAll("article")).IsEmpty();
	}

	[Test]
	public async Task Render_WhileLoadingWithACount_ShowsThatManySkeletons()
	{
		var cut = Render<MediaGrid>(parameters => parameters.Add(grid => grid.Loading, true).Add(grid => grid.Skeletons, 20));

		await Assert.That(cut.Find(".media-grid").Children.Length).IsEqualTo(20);
	}

	[Test]
	public async Task Render_OnceLoaded_ShowsTheTilesInTheGrid()
	{
		var cut = Render<MediaGrid>(parameters => parameters.AddChildContent("<article>tile</article>"));

		await Assert.That(cut.Find(".media-grid > article").TextContent).IsEqualTo("tile");
	}
}
