using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Wantlist;

public class WantlistPageTests
{
	[Test]
	public async Task Wantlist_WhenEmpty_SaysWhereATitleGoesAndAddATitleOpensSearch()
	{
		var page = await E2EApp.MemberAt(Routes.Wantlist);

		await Expect(page.GetByText("Your wantlist is empty.")).ToBeVisibleAsync(Wait.Settle);
		await Expect(page.GetByText("Plex watchlist")).ToBeVisibleAsync();
		await Expect(page.Link("Create a feed")).ToBeVisibleAsync();

		await page.ButtonContaining("Add a title").ClickAsync();

		await Expect(page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Search" })).ToBeVisibleAsync();
	}
}
