using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

internal enum PreviewBadge
{
	Total,
	Included,
	Kept,
	Bypassed,
	Filtered,
	Skipped,
	Unresolved,
}

internal static class FeedEditor
{
	internal const string ListIdentifier = "2236";

	internal const string ResultRow = ".preview-main-results .search-result-row";

	internal const string UnresolvedRow = ".search-result-row.is-unresolved";

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
		await page.Button("Mdblist").First.ClickAsync();
		await page.WaitForTimeoutAsync(500);
	}

	internal static async Task FillAsync(IPage page, string selector, string value)
	{
		await page.Locator(selector).FillAsync(value);
		await page.Locator(selector).PressAsync("Tab");
	}

	internal static Task TestAsync(IPage page) => TestButton(page).ClickAsync();

	internal static Task ApplyAsync(IPage page) => ApplyButton(page).ClickAsync();

	internal static async Task PreviewLoadedAsync(IPage page) => await Expect(SaveButton(page)).ToBeEnabledAsync(Wait.Preview);

	internal static async Task SaveAsync(IPage page)
	{
		await PreviewLoadedAsync(page);
		await SaveButton(page).ClickAsync();
	}

	internal static ILocator SaveButton(IPage page) => page.Button("Save").First;

	internal static ILocator ApplyButton(IPage page) => page.Button("Apply").First;

	internal static ILocator TestButton(IPage page) => page.Button("Test").First;

	internal static ILocator Rows(IPage page) => page.Locator(ResultRow);

	internal static async Task<IReadOnlyList<string>> RowTitlesAsync(IPage page) =>
		[.. (await page.Locator(RowTitle).AllTextContentsAsync()).Select(title => title.Trim())];

	internal static ILocator Badge(IPage page, PreviewBadge badge) => page.Locator(".stat-badges-row .preview-filter-badge").Nth(Position(badge));

	internal static ILocator Toggle(IPage page, string label) => page.Field(label).Switch;

	internal static ILocator Option(IPage page, string label, string value) => page.Field(label).Button(value);

	internal static async Task AddConditionAsync(IPage page, string field, string value)
	{
		await page.ButtonContaining("Add condition").ClickAsync();
		await page.Locator("[role='group'][aria-label='Filter condition']").Combobox.First.ClickAsync();
		await page.Option(field).ClickAsync();
		await FillAsync(page, "input[placeholder='Enter value...']", value);
		await page.WaitForTimeoutAsync(FilterBuilderDebounceMs * 3);
	}

	internal static async Task OpenOptionalSectionAsync(IPage page, string heading)
	{
		await page.GetByText(heading).First.ClickAsync();
		await page.WaitForTimeoutAsync(500);
	}

	internal static Task<IReadOnlyList<string>> ServedTitlesAsync() => MdblistFeed.ServedTitlesAsync(ListIdentifier);

	private static int Position(PreviewBadge badge) =>
		badge switch
		{
			PreviewBadge.Total => 0,
			PreviewBadge.Included => 1,
			PreviewBadge.Kept => 2,
			PreviewBadge.Bypassed => 3,
			PreviewBadge.Filtered => 4,
			PreviewBadge.Skipped => 5,
			PreviewBadge.Unresolved => 6,
		};
}
