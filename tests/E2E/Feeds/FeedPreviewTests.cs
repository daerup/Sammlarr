using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedPreviewTests
{
	private const string OptionalSection = "Filters, Sorting & Limits";

	private const string SearchBox = "input[placeholder='Search media...']";

	private const int Limit = 4;

	[Test]
	public async Task Search_WhenTyped_NarrowsThePreviewRows()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await PreviewAsync(page, "Search");

		var titles = await FeedEditor.RowTitlesAsync(page);
		var term = titles[0];
		var expected = titles.Count(title => title.Contains(term, StringComparison.OrdinalIgnoreCase));

		await page.Locator(SearchBox).EnterAsync(term);

		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(expected);
		await Assert.That(await FeedEditor.RowTitlesAsync(page)).Contains(term);

		await page.Locator(SearchBox).EnterAsync(Guid.NewGuid().ToString("N"));

		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(0);
		await Expect(page.GetByText("No results match")).ToBeVisibleAsync();

		await page.Locator(SearchBox).EnterAsync(string.Empty);

		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(titles.Count);
	}

	[Test]
	public async Task Badges_WhenCombined_ShowBothGroupsAndTheTotalResets()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await PreviewAsync(page, "Badges");
		var listed = (await FeedEditor.ServedTitlesAsync()).Count;

		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable limit").ClickAsync();
		await FeedEditor.FillAsync(page, "#limit", Limit.ToString(CultureInfo.InvariantCulture));
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var included = int.Parse(await FeedEditor.Badge(page, PreviewBadge.Included).InnerTextAsync(), CultureInfo.InvariantCulture);

		await FeedEditor.Badge(page, PreviewBadge.Skipped).ClickAsync();
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(listed - Limit);

		await FeedEditor.Badge(page, PreviewBadge.Included).ClickAsync();
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(listed - Limit + included);
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveClassAsync(new Regex("is-active"));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).ToHaveClassAsync(new Regex("is-active"));

		await FeedEditor.Badge(page, PreviewBadge.Total).ClickAsync();

		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(listed);
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).Not.ToHaveClassAsync(new Regex("is-active"));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).Not.ToHaveClassAsync(new Regex("is-active"));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Total)).ToHaveClassAsync(new Regex("is-active"));
	}

	[Test]
	public async Task RefreshAssets_WhenConfirmed_RefetchesDetails()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await PreviewAsync(page, "Refresh");
		var served = await FeedEditor.ServedTitlesAsync();
		var warm = await E2EApp.RequestCountAsync("/library/metadata/");

		await RefreshAsync(page);

		await E2EApp.WaitForRequestsBeyondAsync("/library/metadata/", warm);
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(served.Count);
	}

	[Test]
	public async Task RefreshAssets_WhenThePreviewHasUnmatchedItems_StillRefreshes()
	{
		const string identifier = "e2e/refresh-unmatched";
		await using var stub = await E2EApp.StubAsync($"/lists/{identifier}/items", 200, UnmatchedList(3));

		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.SelectMdblistAsync(page);
		await FeedEditor.FillAsync(page, "#name", TestData.Name("Unmatched"));
		await FeedEditor.FillAsync(page, "#list-identifier", identifier);
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
		await Expect(page.Locator(FeedEditor.UnmatchedRow)).ToHaveCountAsync(3);
		var listed = await E2EApp.RequestCountAsync($"/lists/{identifier}/items");

		await RefreshAsync(page);

		await E2EApp.WaitForRequestsBeyondAsync($"/lists/{identifier}/items", listed);
		await Expect(page.Locator(FeedEditor.UnmatchedRow)).ToHaveCountAsync(3);

		await FeedEditor.FillAsync(page, SearchBox, "Unmatched 1");
		await Expect(page.Locator(FeedEditor.UnmatchedRow)).ToHaveCountAsync(1, Wait.Settle);
		await FeedEditor.FillAsync(page, SearchBox, string.Empty);
		await Expect(page.Locator(FeedEditor.UnmatchedRow)).ToHaveCountAsync(3, Wait.Settle);
	}

	private static string UnmatchedList(int count) =>
		$"{{\"movies\":[{string.Join(',', Enumerable.Range(0, count).Select(index => $"{{\"title\":\"E2E Unmatched {index}\",\"release_year\":2024}}"))}],\"shows\":[]}}";

	private static async Task RefreshAsync(IPage page)
	{
		var dialog = page.Heading("Refresh assets?");
		await page.GetByLabel("Refresh assets").DispatchEventAsync("click");
		await Expect(dialog).ToBeVisibleAsync();
		await page.Button("Refresh").ClickAsync();
		await Expect(dialog).ToBeHiddenAsync(Wait.Settle);
		await FeedEditor.PreviewLoadedAsync(page);
	}

	private static async Task PreviewAsync(IPage page, string scenario)
	{
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, $"E2E {scenario} {Guid.NewGuid().ToString("N")[..6]}");
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
	}
}
