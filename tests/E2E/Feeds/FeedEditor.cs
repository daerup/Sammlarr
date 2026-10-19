using System.Globalization;
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
}

internal static class FeedEditor
{
	internal const string ListIdentifier = "2236";

	internal const string StoredListIdentifier = "garycrawfordgc/top-movies-of-the-week";

	internal const string ResultRow = ".preview-main-results .search-result-row";

	internal const string UnmatchedRow = ".search-result-row.is-unmatched";

	private const string RowTitle = $"{ResultRow} .media-title-text";

	private const string RowRating = $"{ResultRow} .rating-badge-wrap";

	private const int ReplanPauseMs = 300;

	internal static async Task ConfigureAsync(IPage page, string name)
	{
		await SelectMdblistAsync(page);
		await FillAsync(page, "#name", name);
		await FillAsync(page, "#list-identifier", ListIdentifier);
	}

	internal static async Task SelectMdblistAsync(IPage page)
	{
		await (await ProviderOptionAsync(page, "MDBList")).ClickAsync();
		await page.WaitForTimeoutAsync(500);
	}

	internal static async Task<ILocator> ProviderOptionAsync(IPage page, string provider)
	{
		await page.Locator(".provider-trigger").ClickAsync();
		return page.OptionContaining(provider);
	}

	internal static Task FillAsync(IPage page, string selector, string value) => page.Locator(selector).EnterAsync(value);

	internal static Task LoadAsync(IPage page) =>
		Expect(page.Locator("[data-preview-state='idle']")).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = Wait.Preview });

	internal static async Task CaughtUpAsync(IPage page)
	{
		var preview = page.Locator("[data-preview-current]");
		try
		{
			await Expect(preview).ToHaveAttributeAsync("data-preview-current", "false", new LocatorAssertionsToHaveAttributeOptions { Timeout = 1_000 });
		}
		catch (PlaywrightException) { }

		await Expect(preview).ToHaveAttributeAsync("data-preview-current", "true", new LocatorAssertionsToHaveAttributeOptions { Timeout = Wait.Preview });
	}

	internal static Task PreviewLoadedAsync(IPage page) => Expect(page.Locator("[data-preview-state='success']")).ToBeVisibleAsync(Wait.Preview);

	internal static async Task SaveAndCloseAsync(IPage page)
	{
		await PreviewLoadedAsync(page);
		await Expect(SaveButton(page)).ToBeEnabledAsync(Wait.Preview);
		await SaveButton(page).ClickAsync();
		await Expect(SavedStatus(page)).ToBeVisibleAsync(Wait.Settle);
		await CloseButton(page).ClickAsync();
	}

	internal static ILocator SaveButton(IPage page) => page.Button("Save").First;

	internal static ILocator CloseButton(IPage page) => page.Button("Close").First;

	internal static ILocator SavedStatus(IPage page) => page.Locator("[data-save-status='saved']");

	internal static ILocator SaveStatus(IPage page) => page.Locator("[data-save-status]");

	internal static ILocator PendingIdentifier(IPage page) => page.Locator("[data-identifier-pending]");

	internal static ILocator Rows(IPage page) => page.Locator(ResultRow);

	internal static async Task<IReadOnlyList<string>> RowTitlesAsync(IPage page)
	{
		await Expect(page.Locator(RowTitle).First).ToBeVisibleAsync();
		return [.. (await page.Locator(RowTitle).AllTextContentsAsync()).Select(title => title.Trim())];
	}

	internal static async Task<IReadOnlyList<double>> RowRatingsAsync(IPage page)
	{
		await Expect(page.Locator(RowRating).First).ToBeVisibleAsync();
		return [.. (await page.Locator(RowRating).AllTextContentsAsync()).Select(rating => double.Parse(rating.Trim(), CultureInfo.InvariantCulture))];
	}

	internal static ILocator Badge(IPage page, PreviewBadge badge) => page.Locator(".stat-badges-row .preview-filter-badge").Nth(Position(badge));

	internal static ILocator Toggle(IPage page, string label) => page.Field(label).Switch;

	internal static ILocator Option(IPage page, string label, string value) => page.Field(label).Radio(value);

	internal static ILocator RunCount(IPage page) => page.Locator("input[aria-label='Runs missing before removal']");

	internal static async Task AddConditionAsync(IPage page, string field, string value)
	{
		await page.ButtonContaining("Add condition").ClickAsync();
		await page.Locator("[role='group'][aria-label='Filter condition']").Combobox.First.ClickAsync();
		await page.Option(field).ClickAsync();
		await FillAsync(page, "input[placeholder='Enter value...']", value);
		await page.WaitForTimeoutAsync(ReplanPauseMs * 3);
	}

	internal static async Task AddNumberConditionAsync(IPage page, string field, string comparison, string value)
	{
		await page.ButtonContaining("Add condition").ClickAsync();
		var condition = page.Locator("[role='group'][aria-label='Filter condition']").Last;
		await condition.Combobox.First.ClickAsync();
		await page.Option(field).ClickAsync();
		await condition.Combobox.Nth(1).ClickAsync();
		await page.Option(comparison).ClickAsync();
		await condition.Locator($"input[aria-label='{field}']").EnterAsync(value);
		await page.WaitForTimeoutAsync(ReplanPauseMs * 3);
	}

	internal static ILocator RatingStar(IPage page) => page.Locator("button.field-segment.is-gold").First;

	internal static async Task PutFirstAsync(IPage page, string source, int steps)
	{
		var grip = page.Locator($".rating-picker-grip[aria-label^='Move {source};']");
		var rank = page.Locator(".rating-picker-item").Filter(new LocatorFilterOptions { Has = grip }).Locator(".rating-picker-rank");
		var start = int.Parse(await rank.InnerTextAsync(), CultureInfo.InvariantCulture);
		await grip.FocusAsync();
		for (var step = 1; step <= steps; step++)
		{
			await page.Keyboard.PressAsync("Alt+ArrowUp");
			await Expect(rank).ToHaveTextAsync((start - step).ToString(CultureInfo.InvariantCulture));
			await Expect(grip).ToBeFocusedAsync();
		}
	}

	internal static async Task OpenOptionalSectionAsync(IPage page, string heading)
	{
		await page.GetByText(heading).First.ClickAsync();
		await page.WaitForTimeoutAsync(500);
	}

	internal static Task<IReadOnlyList<string>> ServedTitlesAsync() => MdblistFeed.ServedTitlesAsync(StoredListIdentifier);

	private static int Position(PreviewBadge badge) =>
		badge switch
		{
			PreviewBadge.Total => 0,
			PreviewBadge.Included => 1,
			PreviewBadge.Kept => 2,
			PreviewBadge.Bypassed => 3,
			PreviewBadge.Filtered => 4,
			PreviewBadge.Skipped => 5,
		};
}
