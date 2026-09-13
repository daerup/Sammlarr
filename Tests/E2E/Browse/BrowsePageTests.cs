using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Browse;

public class BrowsePageTests
{
	[Test]
	public async Task Browse_WhenAuthenticatedWithStubbedTrending_ShowsTheStubbedTitle()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/browse");

		await Expect(page.GetByText(WireMockStubs.ExpectedTrendingTitle())).ToBeVisibleAsync();
	}

	[Test]
	public async Task Browse_WhenAddingATrendingItem_ItAppearsInTheWatchlist()
	{
		var title = WireMockStubs.ExpectedTrendingTitle();
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/browse");
		await page.GetByText(title).First.ClickAsync();
		var addButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add to Watchlist" });
		await addButton.ClickAsync();
		await addButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

		await page.VisitAsync("/watchlist");

		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true }))
			.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
	}
}
