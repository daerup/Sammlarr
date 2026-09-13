using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Watchlist;

public class WatchlistPageTests
{
	[Test]
	public async Task Watchlist_WhenAuthenticated_ShowsTheWatchlistHeading()
	{
		var page = await E2EApp.AdminAt(Routes.Watchlist);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Expect(page.Heading("Plex Watchlist")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Watchlist_WhenAuthenticated_OffersTheSearchShortcut()
	{
		var page = await E2EApp.AdminAt(Routes.Watchlist);

		await Expect(page.GetByText("Ctrl+K").First).ToBeVisibleAsync();
	}
}
