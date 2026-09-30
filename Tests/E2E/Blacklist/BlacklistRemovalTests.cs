using System.Globalization;
using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Blacklist;

public class BlacklistRemovalTests
{
	[Test]
	public async Task Unblacklisted_WhenRemovedFromTheDetailSheet_StopsBypassingTheFeedPreview()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);
		await PreviewAsync(page);

		var bypassed = await CountAsync(page, PreviewBadge.Bypassed);
		var included = await CountAsync(page, PreviewBadge.Included);
		await FeedEditor.Badge(page, PreviewBadge.Included).ClickAsync();
		var target = (await FeedEditor.RowTitlesAsync(page))[0];

		await FeedEditor.Rows(page).First.Locator(".search-result-toggle-btn[style*='destructive']").ClickAsync();

		await Expect(FeedEditor.Badge(page, PreviewBadge.Bypassed)).ToHaveTextAsync(TestData.Count(bypassed + 1));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(included - 1));

		await page.VisitAsync(Routes.Blacklist);
		await Expect(page.Heading(target)).ToBeVisibleAsync(Wait.Settle);
		await page.Heading(target).ClickAsync();
		await page.ButtonContaining("Remove from Blacklist").ClickAsync();

		await Expect(page.Heading(target)).ToBeHiddenAsync(Wait.Settle);

		await page.VisitAsync(Routes.CreateFeed);
		await PreviewAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Bypassed)).ToHaveTextAsync(TestData.Count(bypassed));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(included));
	}

	private static async Task PreviewAsync(IPage page)
	{
		await FeedEditor.ConfigureAsync(page, TestData.Name("Unblacklist"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
	}

	private static async Task<int> CountAsync(IPage page, PreviewBadge badge) =>
		int.Parse((await FeedEditor.Badge(page, badge).InnerTextAsync()).Trim(), CultureInfo.InvariantCulture);
}
