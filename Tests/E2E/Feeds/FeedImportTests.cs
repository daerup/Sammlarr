using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

[NotInParallel]
public class FeedImportTests
{
	private const string ListIdentifier = "2236";
	private const string ResultRow = ".search-result-row";

	[Test]
	public async Task Save_WhenNameIsMissing_IsNotOffered()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await SelectMdblistAsync(page);
		await page.Locator("#list-identifier").FillAsync(ListIdentifier);

		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true })).ToBeHiddenAsync();
	}

	[Test]
	public async Task Test_WhenGivenAnMdblistList_LoadsTheListItemsIntoThePreview()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await ConfigureFeedAsync(page, "E2E Preview Feed");
		await TestAsync(page);
		await PreviewLoadedAsync(page);

		var listed = await SourceListSizeAsync();
		await Expect(page.GetByText("Run a test to preview the media items from your list.")).ToBeHiddenAsync();
		await Expect(page.Locator(".stat-badges-row .preview-filter-badge").First).ToHaveTextAsync(listed.ToString(CultureInfo.InvariantCulture));
		await Expect(page.Locator(ResultRow)).ToHaveCountAsync(listed);
		await Expect(page.Locator($"{ResultRow}.is-unresolved")).ToHaveCountAsync(0);
		await Expect(page.Locator($"{ResultRow} .search-result-poster img")).ToHaveCountAsync(listed);
	}

	[Test]
	public async Task Save_WhenPreviewSucceeded_PersistsTheFeedIntoTheList()
	{
		var name = $"E2E Feed {Guid.NewGuid().ToString("N")[..6]}";
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await ConfigureFeedAsync(page, name);
		await TestAsync(page);

		await SaveAsync(page);

		await Expect(page.GetByText(name)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
	}

	[Test]
	public async Task Delete_WhenConfirmed_RemovesTheFeedFromTheList()
	{
		var name = $"E2E Doomed {Guid.NewGuid().ToString("N")[..6]}";
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await ConfigureFeedAsync(page, name);
		await TestAsync(page);
		await SaveAsync(page);
		await Expect(page.GetByText(name)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });

		await page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name }).GetByRole(AriaRole.Button).Last.ClickAsync();
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = $"Delete \"{name}\"?" })).ToBeVisibleAsync();
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Delete", Exact = true }).Last.ClickAsync();

		await Expect(page.GetByRole(AriaRole.Cell, new PageGetByRoleOptions { Name = name }))
			.ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions { Timeout = 30000 });
	}

	private static async Task SaveAsync(IPage page)
	{
		await PreviewLoadedAsync(page);
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).First.ClickAsync();
	}

	private static async Task PreviewLoadedAsync(IPage page) =>
		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).First)
			.ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 60000 });

	private static async Task<int> SourceListSizeAsync() =>
		JsonNode.Parse(await E2EApp.ServedBodyAsync($"/lists/{ListIdentifier}/items"))!["pagination"]!["total"]!.GetValue<int>();

	private static async Task ConfigureFeedAsync(IPage page, string name)
	{
		await SelectMdblistAsync(page);
		await Fill(page, "#name", name);
		await Fill(page, "#list-identifier", ListIdentifier);
	}

	private static async Task Fill(IPage page, string selector, string value)
	{
		await page.Locator(selector).FillAsync(value);
		await page.Locator(selector).PressAsync("Tab");
	}

	private static async Task SelectMdblistAsync(IPage page)
	{
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Mdblist", Exact = true }).First.ClickAsync();
		await page.WaitForTimeoutAsync(500);
	}

	private static async Task TestAsync(IPage page) =>
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Test", Exact = true }).ClickAsync();
}
