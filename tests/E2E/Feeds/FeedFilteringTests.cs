using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedFilteringTests
{
	private const string OptionalSection = "Filters, Sorting & Limits";

	private const int Limit = 3;

	[Test]
	public async Task SlotPolicy_WhenPreserved_DoesNotConsumeTheLimit()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Slots"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var listed = (await FeedEditor.ServedTitlesAsync()).Count;
		await FeedEditor.Rows(page).First.Button("Add to blocklist").ClickAsync();
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable limit").ClickAsync();
		await FeedEditor.FillAsync(page, "#limit", TestData.Count(Limit));
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).ToHaveTextAsync(TestData.Count(listed - Limit));

		await FeedEditor.Option(page, "Blocklist slot policy", "Preserved").ClickAsync();
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).ToHaveTextAsync(TestData.Count(listed - Limit - 1));

		await FeedEditor.Rows(page).First.Button("Remove from blocklist").ClickAsync();

		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).ToHaveTextAsync(TestData.Count(listed - Limit));
	}

	[Test]
	public async Task Filter_WhenSavedAndReopened_IsRestoredInTheEditor()
	{
		var name = TestData.Name("Restored");
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

		var card = page.FeedCard(name);
		await Expect(card).ToBeVisibleAsync(Wait.Settle);
		await card.Labelled("Edit").ClickAsync();

		await Expect(FeedEditor.Toggle(page, "Enable filters")).ToHaveAttributeAsync("aria-checked", "true");
		var condition = page.Locator("[role='group'][aria-label='Filter condition']");
		await Expect(condition.GetByRole(AriaRole.Combobox).First).ToHaveTextAsync(new Regex("Title"));
		await Expect(page.Locator("input[placeholder='Enter value...']")).ToHaveValueAsync(kept);
		var listed = (await FeedEditor.ServedTitlesAsync()).Count;
		await Expect(FeedEditor.Badge(page, PreviewBadge.Filtered)).ToHaveTextAsync(TestData.Count(listed - 1), Wait.Preview);
	}

	[Test]
	public async Task Limit_WhenEnabledButEmpty_BlocksTheTest()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("NoLimit"));
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);

		await FeedEditor.Toggle(page, "Enable limit").ClickAsync();

		await Expect(page.Locator("#limit")).ToHaveAttributeAsync("aria-invalid", "true");
		await Expect(FeedEditor.TestButton(page)).ToBeDisabledAsync();

		await FeedEditor.FillAsync(page, "#limit", TestData.Count(Limit));

		await Expect(page.Locator("#limit")).Not.ToHaveAttributeAsync("aria-invalid", "true");
		await Expect(FeedEditor.TestButton(page)).ToBeEnabledAsync();
	}
}
