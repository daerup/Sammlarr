using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Wantlist;

public class WantlistPageTests
{
	[Test]
	public async Task Wantlist_WhenAuthenticated_ShowsTheWantlistHeading()
	{
		var page = await E2EApp.AdminAt(Routes.Wantlist);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Expect(page.Heading("Wantlist")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Wantlist_WhenAuthenticated_OffersTheSearchShortcut()
	{
		var page = await E2EApp.AdminAt(Routes.Wantlist);

		await Expect(page.GetByText("Ctrl+K").First).ToBeVisibleAsync();
	}

	[Test]
	public async Task Wantlist_WhenEmpty_SaysWhereATitleGoesAndAddATitleOpensSearch()
	{
		var page = await E2EApp.MemberAt(Routes.Wantlist);

		await Expect(page.GetByText("Your wantlist is empty.")).ToBeVisibleAsync(Wait.Settle);
		await Expect(page.GetByText("go to your Plex watchlist on the next sync")).ToBeVisibleAsync();
		await Expect(page.Link("Create a feed")).ToBeVisibleAsync();

		await page.ButtonContaining("Add a title").ClickAsync();

		await Expect(page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Search" })).ToBeVisibleAsync();
	}
}
