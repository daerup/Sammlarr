using System.Text.Json.Nodes;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

internal enum PreviewBadge
{
	Total,
	Included,
	Bypassed,
	Filtered,
	Skipped,
	Unresolved,
}

internal static class FeedEditor
{
	internal const string ListIdentifier = "2236";

	internal const string ResultRow = ".search-result-row";

	private const string RowTitle = $"{ResultRow} .media-title-text";

	private const int FilterBuilderDebounceMs = 300;

	internal static async Task ConfigureAsync(IPage page, string name)
	{
		await SelectMdblistAsync(page);
		await FillAsync(page, "#name", name);
		await FillAsync(page, "#list-identifier", ListIdentifier);
	}

	internal static async Task SelectMdblistAsync(IPage page)
	{
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Mdblist", Exact = true }).First.ClickAsync();
		await page.WaitForTimeoutAsync(500);
	}

	internal static async Task FillAsync(IPage page, string selector, string value)
	{
		await page.Locator(selector).FillAsync(value);
		await page.Locator(selector).PressAsync("Tab");
	}

	internal static Task TestAsync(IPage page) => Click(page, "Test");

	internal static Task ApplyAsync(IPage page) => Click(page, "Apply");

	internal static async Task PreviewLoadedAsync(IPage page) =>
		await Expect(SaveButton(page)).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 60000 });

	internal static async Task SaveAsync(IPage page)
	{
		await PreviewLoadedAsync(page);
		await SaveButton(page).ClickAsync();
	}

	internal static ILocator SaveButton(IPage page) => Named(page, "Save");

	internal static ILocator ApplyButton(IPage page) => Named(page, "Apply");

	internal static ILocator TestButton(IPage page) => Named(page, "Test");

	private static ILocator Named(IPage page, string name) => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true }).First;

	internal static ILocator Rows(IPage page) => page.Locator(ResultRow);

	internal static async Task<IReadOnlyList<string>> RowTitlesAsync(IPage page) =>
		[.. (await page.Locator(RowTitle).AllTextContentsAsync()).Select(title => title.Trim())];

	internal static ILocator Badge(IPage page, PreviewBadge badge) => page.Locator(".stat-badges-row .preview-filter-badge").Nth(Position(badge));

	internal static ILocator Toggle(IPage page, string label) =>
		page.Locator("[data-slot='field']").Filter(new LocatorFilterOptions { HasText = label }).GetByRole(AriaRole.Switch);

	internal static ILocator Option(IPage page, string label, string value) =>
		page.Locator("[data-slot='field']")
			.Filter(new LocatorFilterOptions { HasText = label })
			.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = value, Exact = true });

	internal static async Task AddConditionAsync(IPage page, string field, string value)
	{
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add condition" }).ClickAsync();
		var condition = page.Locator("[role='group'][aria-label='Filter condition']");
		await condition.GetByRole(AriaRole.Combobox).First.ClickAsync();
		await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = field, Exact = true }).ClickAsync();
		await FillAsync(page, "input[placeholder='Enter value...']", value);
		await page.WaitForTimeoutAsync(FilterBuilderDebounceMs * 3);
	}

	internal static async Task OpenOptionalSectionAsync(IPage page, string heading)
	{
		await page.GetByText(heading).First.ClickAsync();
		await page.WaitForTimeoutAsync(500);
	}

	internal static Task<IReadOnlyList<string>> ServedTitlesAsync() => MdblistFeed.ServedTitlesAsync(ListIdentifier);

	private static Task Click(IPage page, string name) => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true }).ClickAsync();

	private static int Position(PreviewBadge badge) =>
		badge switch
		{
			PreviewBadge.Total => 0,
			PreviewBadge.Included => 1,
			PreviewBadge.Bypassed => 2,
			PreviewBadge.Filtered => 3,
			PreviewBadge.Skipped => 4,
			PreviewBadge.Unresolved => 5,
		};
}
