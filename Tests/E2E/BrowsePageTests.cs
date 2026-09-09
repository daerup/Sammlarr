using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests;

public class BrowsePageTests
{
	[Test]
	public async Task Browse_WhenAuthenticatedWithStubbedTrending_ShowsTheStubbedTitle()
	{
		var page = await AspireApp.OpenAuthenticatedPageAsync();

		await page.GotoAsync("/browse");

		await Expect(page.GetByText(WireMockStubs.ExpectedTrendingTitle())).ToBeVisibleAsync();
	}

	[Test]
	public async Task Browse_WhenAddingATrendingItem_ItAppearsInTheWatchlist()
	{
		var title = WireMockStubs.ExpectedTrendingTitle();
		var page = await AspireApp.OpenAuthenticatedPageAsync();

		await page.GotoAsync("/browse");
		await page.GetByText(title).First.ClickAsync();
		var addButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add to Watchlist" });
		await addButton.ClickAsync();
		await addButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

		await page.GotoAsync("/watchlist");

		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Bulk edit" }))
			.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
	}
}
