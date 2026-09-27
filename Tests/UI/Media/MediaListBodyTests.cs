using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaListBodyTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private IRenderedComponent<MediaListBody> RenderBody(
		bool isLoading = false,
		string? errorMessage = null,
		int? totalCount = 0,
		int visibleCount = 0,
		bool showFilters = false
	) =>
		Render<MediaListBody>(parameters =>
			parameters
				.Add(body => body.IsLoading, isLoading)
				.Add(body => body.ErrorMessage, errorMessage)
				.Add(body => body.TotalCount, totalCount)
				.Add(body => body.VisibleCount, visibleCount)
				.Add(body => body.ShowFilters, showFilters)
				.Add<RenderFragment>(body => body.EmptyState, builder => builder.AddMarkupContent(0, "<p>nothing here yet</p>"))
				.Add<RenderFragment>(body => body.Grid, builder => builder.AddMarkupContent(0, "<p>the grid</p>"))
		);

	[Test]
	public async Task Render_WhileLoading_ShowsSkeletonsInsteadOfTheGrid()
	{
		var cut = RenderBody(isLoading: true, totalCount: 5, visibleCount: 5);

		await Assert.That(cut.Markup).DoesNotContain("the grid");
		await Assert.That(cut.Markup).DoesNotContain("nothing here yet");
	}

	[Test]
	public async Task Render_WithAnError_ShowsTheMessageAndARetry()
	{
		var cut = RenderBody(errorMessage: "Failed to load blacklist: boom", totalCount: 5, visibleCount: 5);

		await Assert.That(cut.Markup).Contains("Failed to load blacklist: boom");
		await Assert.That(cut.Markup).Contains("Retry");
		await Assert.That(cut.Markup).DoesNotContain("the grid");
	}

	[Test]
	public async Task Retry_WhenPressed_RaisesTheCallback()
	{
		var retried = false;
		var cut = Render<MediaListBody>(parameters =>
			parameters
				.Add(body => body.ErrorMessage, "boom")
				.Add(body => body.OnRetry, () => retried = true)
				.Add<RenderFragment>(body => body.EmptyState, builder => builder.AddMarkupContent(0, "<p>nothing here yet</p>"))
				.Add<RenderFragment>(body => body.Grid, builder => builder.AddMarkupContent(0, "<p>the grid</p>"))
		);

		await cut.Find("button").ClickAsync();

		await Assert.That(retried).IsTrue();
	}

	[Test]
	public async Task Render_WhenTheListIsEmpty_ShowsTheCallersEmptyState()
	{
		var cut = RenderBody(totalCount: 0);

		await Assert.That(cut.Markup).Contains("nothing here yet");
		await Assert.That(cut.Markup).DoesNotContain("the grid");
	}

	[Test]
	public async Task Render_WhenFiltersHideEverything_ShowsTheNoMatchStateWithTheHiddenCount()
	{
		var cut = RenderBody(totalCount: 42, visibleCount: 0);

		await Assert.That(cut.Markup).Contains("Nothing matches these filters.");
		await Assert.That(cut.Markup).Contains("42 titles are hidden");
		await Assert.That(cut.Markup).DoesNotContain("nothing here yet");
	}

	[Test]
	public async Task Render_WithVisibleItems_ShowsTheGrid()
	{
		var cut = RenderBody(totalCount: 42, visibleCount: 42);

		await Assert.That(cut.Markup).Contains("the grid");
		await Assert.That(cut.Markup).DoesNotContain("Nothing matches these filters.");
	}

	[Test]
	public async Task Render_BeforeTheListHasLoaded_ShowsSkeletonsRatherThanABlankBody()
	{
		var cut = RenderBody(totalCount: null);

		await Assert.That(cut.Markup).DoesNotContain("the grid");
		await Assert.That(cut.Markup).DoesNotContain("nothing here yet");
		await Assert.That(cut.Markup.Trim()).IsNotEmpty();
	}

	[Test]
	public async Task Render_WhenALoadFailedBeforeAnyData_ShowsTheErrorRatherThanEndlessSkeletons()
	{
		var cut = RenderBody(errorMessage: "boom", totalCount: null);

		await Assert.That(cut.Markup).Contains("boom");
		await Assert.That(cut.Markup).Contains("Retry");
	}
}
