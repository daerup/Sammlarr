using System.Globalization;
using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Blacklist;

public class BlacklistTests
{
	[Test]
	public async Task Blacklisted_WhenPreviewingAFeed_IsBypassedAndListedOnTheBlacklistPage()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Blacklist"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var titles = await FeedEditor.RowTitlesAsync(page);
		var target = titles[0];
		await Expect(FeedEditor.Badge(page, PreviewBadge.Bypassed)).ToHaveTextAsync("0");

		await FeedEditor.Rows(page).First.Locator(".search-result-toggle-btn[style*='destructive']").ClickAsync();

		await Expect(FeedEditor.Badge(page, PreviewBadge.Bypassed)).ToHaveTextAsync("1");
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync((titles.Count - 1).ToString(CultureInfo.InvariantCulture));

		await page.VisitAsync("/blacklist");
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = target, Exact = true })).ToBeVisibleAsync(Wait.Settle);

		await page.ButtonContaining("Bulk edit").ClickAsync();
		await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Select all on this page" }).ClickAsync();
		await Expect(page.GetByText("1 selected")).ToBeVisibleAsync();
		await page.Button("Remove").ClickAsync();
		await page.AlertDialog.ButtonContaining("Remove").ClickAsync();

		await Expect(page.GetByText("Nothing blacklisted.")).ToBeVisibleAsync(Wait.Settle);
	}
}
