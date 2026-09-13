using System.Globalization;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

[NotInParallel]
public class FeedImportTests
{
	[Test]
	public async Task Save_WhenNameIsMissing_IsNotOffered()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await FeedEditor.SelectMdblistAsync(page);
		await page.Locator("#list-identifier").FillAsync(FeedEditor.ListIdentifier);

		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true })).ToBeHiddenAsync();
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
		var name = $"E2E Feed {Guid.NewGuid().ToString("N")[..6]}";
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.TestAsync(page);

		await FeedEditor.SaveAsync(page);

		await Expect(page.GetByText(name)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
	}

	[Test]
	public async Task Delete_WhenConfirmed_RemovesTheFeedFromTheList()
	{
		var name = $"E2E Doomed {Guid.NewGuid().ToString("N")[..6]}";
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);
		await Expect(page.GetByText(name)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });

		await page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name }).GetByRole(AriaRole.Button).Last.ClickAsync();
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = $"Delete \"{name}\"?" })).ToBeVisibleAsync();
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Delete", Exact = true }).Last.ClickAsync();

		await Expect(page.GetByRole(AriaRole.Cell, new PageGetByRoleOptions { Name = name }))
			.ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions { Timeout = 30000 });
	}
}
