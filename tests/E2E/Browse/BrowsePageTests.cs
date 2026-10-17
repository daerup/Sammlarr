using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Browse;

public class BrowsePageTests
{
	[Test]
	public async Task Browse_WhenAddingATrendingItem_TheWantlistGainsIt()
	{
		var page = await E2EApp.AdminAt(Routes.Browse);
		var tile = TrendingTiles.Resolved(page, TrendingTiles.Title);
		await Expect(tile).ToBeVisibleAsync(Wait.Settle);
		await tile.ClickAsync();

		var addButton = page.ButtonContaining("Add to Wantlist");
		await Expect(addButton).ToBeVisibleAsync(Wait.Settle);
		await addButton.ClickAsync();
		await addButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

		await page.VisitAsync(Routes.Wantlist);

		await Expect(page.Heading(TrendingTiles.Title)).ToBeVisibleAsync(Wait.Settle);

		await page.VisitAsync(Routes.Browse);
		await tile.ClickAsync();
		await page.Button("Remove from wantlist").ClickAsync();
		await Expect(addButton).ToBeVisibleAsync(Wait.Settle);
		await page.VisitAsync(Routes.Wantlist);

		await Expect(page.Heading("Wantlist")).ToBeVisibleAsync(Wait.Settle);
		await Expect(page.Heading(TrendingTiles.Title)).ToBeHiddenAsync(Wait.Settle);
	}
}
