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
		await FeedEditor.ConfigureAsync(page, $"E2E Blacklist {Guid.NewGuid().ToString("N")[..6]}");
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
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = target, Exact = true }))
			.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });

		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Bulk edit" }).ClickAsync();
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Select all" }).ClickAsync();
		await Expect(page.GetByText("1 of 1 selected")).ToBeVisibleAsync();
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Remove from Blacklist (1)" }).ClickAsync();

		await Expect(page.GetByText("Nothing blacklisted.")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
	}
}
