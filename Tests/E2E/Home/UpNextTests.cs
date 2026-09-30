using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Home;

public class UpNextTests
{
	[Test]
	public async Task UpNext_WhenAFeedIsScheduled_NamesItsProviderInTheLogo()
	{
		var name = TestData.Name("Scheduled");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.OpenOptionalSectionAsync(page, "Sync Frequency");
		await FeedEditor.Option(page, "Trigger", "Scheduled").ClickAsync();
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);
		await Expect(page.FeedCard(name)).ToBeVisibleAsync(Wait.Settle);

		await page.VisitAsync(Routes.Home);

		var row = page.Locator(".row").Filter(new LocatorFilterOptions { HasText = name });
		await Expect(row.GetByRole(AriaRole.Img, new LocatorGetByRoleOptions { Name = "Movie Database List", Exact = true })).ToBeVisibleAsync(Wait.Settle);
	}
}
