using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Browse;

public class BrowsePageTests
{
	[Test]
	public async Task Browse_WhenAuthenticatedWithStubbedTrending_ShowsTheStubbedTitle()
	{
		var page = await E2EApp.AdminAt(Routes.Browse);

		await Expect(page.GetByText(WireMockStubs.ExpectedTrendingTitle())).ToBeVisibleAsync();
	}

	[Test]
	public async Task Browse_WhenAddingATrendingItem_ItAppearsInTheWatchlist()
	{
		var title = WireMockStubs.ExpectedTrendingTitle();
		var page = await E2EApp.AdminAt(Routes.Browse);

		await page.GetByText(title).First.ClickAsync();
		var addButton = page.ButtonContaining("Add to Watchlist");
		await addButton.ClickAsync();
		await addButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

		await page.VisitAsync(Routes.Watchlist);

		await Expect(page.Heading(title)).ToBeVisibleAsync(Wait.Settle);
	}
}
