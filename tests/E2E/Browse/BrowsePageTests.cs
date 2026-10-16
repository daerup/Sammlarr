using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Browse;

public class BrowsePageTests
{
	[Test]
	public async Task Browse_WhenAddingATrendingItem_TheWantlistGainsIt()
	{
		var page = await E2EApp.AdminAt(Routes.Wantlist);
		await Expect(page.Heading("Wantlist")).ToBeVisibleAsync(Wait.Settle);
		var before = await Tiles(page).CountAsync();

		await page.VisitAsync(Routes.Browse);
		await Expect(Resolved(page).First).ToBeVisibleAsync(Wait.Settle);
		var title = (await Resolved(page).First.Locator("h3").InnerTextAsync()).Trim();
		await Resolved(page).First.ClickAsync();

		var addButton = page.ButtonContaining("Add to Wantlist");
		await Expect(addButton).ToBeVisibleAsync(Wait.Settle);
		await addButton.ClickAsync();
		await addButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

		await page.VisitAsync(Routes.Wantlist);

		await Expect(Tiles(page)).ToHaveCountAsync(before + 1, Wait.Settle);

		await page.VisitAsync(Routes.Browse);
		await Resolved(page).Filter(new LocatorFilterOptions { Has = page.Heading(title) }).First.ClickAsync();
		await page.Button("Remove from wantlist").ClickAsync();
		await Expect(addButton).ToBeVisibleAsync(Wait.Settle);
		await page.VisitAsync(Routes.Wantlist);

		await Expect(Tiles(page)).ToHaveCountAsync(before, Wait.Settle);
	}

	private static ILocator Tiles(IPage page) => page.Locator("[data-testid='media-tile']");

	private static ILocator Resolved(IPage page) => page.Locator("[data-testid='media-tile']:not(.tile-unmatched)");
}
