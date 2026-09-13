using System.Globalization;
using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Blacklist;

[NotInParallel]
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
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Bypassed)).ToHaveTextAsync("1");
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync((titles.Count - 1).ToString(CultureInfo.InvariantCulture));

		await page.VisitAsync("/blacklist");
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = target, Exact = true })).ToBeVisibleAsync(Wait.Settle);

		await page.ButtonContaining("Bulk edit").ClickAsync();
		await page.ButtonContaining("Select all").ClickAsync();
		await Expect(page.GetByText("1 of 1 selected")).ToBeVisibleAsync();
		await page.ButtonContaining("Remove from Blacklist (1)").ClickAsync();

		await Expect(page.GetByText("Nothing blacklisted.")).ToBeVisibleAsync(Wait.Settle);
	}
}
