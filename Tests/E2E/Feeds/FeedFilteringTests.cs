using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

[NotInParallel]
public class FeedFilteringTests
{
	private const string OptionalSection = "Filters, Sorting & Limits";

	private const int Limit = 3;

	[Test]
	public async Task SlotPolicy_WhenPreserved_DoesNotConsumeTheLimit()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, Name("Slots"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var listed = (await FeedEditor.ServedTitlesAsync()).Count;
		await FeedEditor.Rows(page).First.Locator(".search-result-toggle-btn[style*='destructive']").ClickAsync();
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable limit").ClickAsync();
		await FeedEditor.FillAsync(page, "#limit", Count(Limit));
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Bypassed)).ToHaveTextAsync(Count(1));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(Count(Limit - 1));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).ToHaveTextAsync(Count(listed - Limit));

		await FeedEditor.Option(page, "Blacklist limit behavior", "Preserved").ClickAsync();
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Bypassed)).ToHaveTextAsync(Count(1));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(Count(Limit));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).ToHaveTextAsync(Count(listed - Limit - 1));

		await FeedEditor.Rows(page).First.Locator(".search-result-toggle-btn[style*='destructive']").ClickAsync();
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Bypassed)).ToHaveTextAsync(Count(0));
	}

	[Test]
	public async Task Filter_WhenSavedAndReopened_IsRestoredInTheEditor()
	{
		var name = Name("Restored");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var kept = (await FeedEditor.RowTitlesAsync(page))[0];
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable filters").ClickAsync();
		await FeedEditor.AddConditionAsync(page, "Title", kept);
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.SaveAsync(page);

		var row = page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name });
		await Expect(row).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
		await row.GetByLabel("Edit").ClickAsync();

		await Expect(FeedEditor.Toggle(page, "Enable filters")).ToHaveAttributeAsync("aria-checked", "true");
		var condition = page.Locator("[role='group'][aria-label='Filter condition']");
		await Expect(condition.GetByRole(AriaRole.Combobox).First).ToHaveTextAsync(new Regex("Title"));
		await Expect(page.Locator("input[placeholder='Enter value...']")).ToHaveValueAsync(kept);
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(Count(1), new LocatorAssertionsToHaveTextOptions { Timeout = 60000 });
	}

	[Test]
	public async Task Limit_WhenEnabledButEmpty_BlocksTheTest()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, Name("NoLimit"));
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);

		await FeedEditor.Toggle(page, "Enable limit").ClickAsync();

		await Expect(page.GetByText("Limit must be a positive whole number.")).ToBeVisibleAsync();
		await Expect(page.Locator("#limit")).ToHaveAttributeAsync("aria-invalid", "true");
		await Expect(FeedEditor.TestButton(page)).ToBeDisabledAsync();

		await FeedEditor.FillAsync(page, "#limit", Count(Limit));

		await Expect(page.GetByText("Limit must be a positive whole number.")).ToBeHiddenAsync();
		await Expect(FeedEditor.TestButton(page)).ToBeEnabledAsync();
	}

	private static string Name(string scenario) => $"E2E {scenario} {Guid.NewGuid().ToString("N")[..6]}";

	private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
