using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Watchlist;

public class WatchlistPageTests
{
	[Test]
	public async Task Watchlist_WhenAuthenticated_ShowsTheWatchlistHeading()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/watchlist");

		await Assert.That(page.Url).DoesNotContain("/signin");
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Plex Watchlist" })).ToBeVisibleAsync();
	}

	[Test]
	public async Task Watchlist_WhenAuthenticated_OffersTheSearchShortcut()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/watchlist");

		await Expect(page.GetByText("Ctrl+K").First).ToBeVisibleAsync();
	}
}
