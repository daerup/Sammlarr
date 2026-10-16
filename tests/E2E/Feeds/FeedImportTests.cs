using System.Globalization;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedImportTests
{
	[Test]
	public async Task Save_WhenNameIsMissing_IsNotOffered()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await FeedEditor.SelectMdblistAsync(page);
		await page.Locator("#list-identifier").EnterAsync(FeedEditor.ListIdentifier);

		await Expect(page.Button("Save")).ToBeHiddenAsync();
	}

	[Test]
	public async Task Test_WhenGivenAnMdblistList_LoadsTheListItemsIntoThePreview()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await FeedEditor.ConfigureAsync(page, "E2E Preview Feed");
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var served = await FeedEditor.ServedTitlesAsync();
		await Expect(FeedEditor.Badge(page, PreviewBadge.Total)).ToHaveTextAsync(served.Count.ToString(CultureInfo.InvariantCulture));
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(served.Count);
		await Expect(page.Locator(FeedEditor.UnmatchedRow)).ToHaveCountAsync(0);
		await Expect(page.Locator($"{FeedEditor.ResultRow} .search-result-poster img")).ToHaveCountAsync(served.Count);
	}
}
