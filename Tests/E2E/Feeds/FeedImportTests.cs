using System.Globalization;
using Microsoft.Playwright;
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
		await page.Locator("#list-identifier").FillAsync(FeedEditor.ListIdentifier);

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
		await Expect(page.GetByText("Run a test to preview the media items from your list.")).ToBeHiddenAsync();
		await Expect(FeedEditor.Badge(page, PreviewBadge.Total)).ToHaveTextAsync(served.Count.ToString(CultureInfo.InvariantCulture));
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(served.Count);
		await Expect(page.Locator($"{FeedEditor.ResultRow}.is-unresolved")).ToHaveCountAsync(0);
		await Expect(page.Locator($"{FeedEditor.ResultRow} .search-result-poster img")).ToHaveCountAsync(served.Count);
	}

	[Test]
	public async Task Save_WhenPreviewSucceeded_PersistsTheFeedIntoTheList()
	{
		var name = TestData.Name("Feed");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.TestAsync(page);

		await FeedEditor.SaveAsync(page);

		await Expect(page.GetByText(name)).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task Delete_WhenConfirmed_RemovesTheFeedFromTheList()
	{
		var name = TestData.Name("Doomed");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);
		await Expect(page.GetByText(name)).ToBeVisibleAsync(Wait.Settle);

		await page.Row(name).GetByRole(AriaRole.Button).Last.ClickAsync();
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = $"Delete \"{name}\"?" })).ToBeVisibleAsync();
		await page.Button("Delete").Last.ClickAsync();

		await Expect(page.GetByRole(AriaRole.Cell, new PageGetByRoleOptions { Name = name })).ToBeHiddenAsync(Wait.Settle);
	}
}
