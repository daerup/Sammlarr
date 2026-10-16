using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Blocklist;

public class BlocklistTests
{
	[Test]
	public async Task Blocklisted_FromTheBrowseDetailSheet_IsListedOnTheBlocklistPageUntilABulkRemoveEmptiesIt()
	{
		var page = await E2EApp.AdminAt(Routes.Browse);
		var tile = page.Locator("[data-testid='media-tile']:not(.tile-unmatched)").First;
		await Expect(tile).ToBeVisibleAsync(Wait.Settle);
		var target = (await tile.Locator("h3").InnerTextAsync()).Trim();

		await tile.ClickAsync();
		await page.Button("Add to blocklist").ClickAsync();
		await Expect(page.Button("Remove from blocklist")).ToBeVisibleAsync();

		await page.VisitAsync(Routes.Blocklist);
		await Expect(page.Heading(target)).ToBeVisibleAsync(Wait.Settle);

		await page.ButtonContaining("Bulk edit").ClickAsync();
		await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Select all on this page" }).ClickAsync();
		await Expect(page.GetByText("1 selected")).ToBeVisibleAsync();
		await page.Button("Remove").ClickAsync();
		await page.AlertDialog.ButtonContaining("Remove").ClickAsync();

		await Expect(page.GetByText("Nothing blocklisted.")).ToBeVisibleAsync(Wait.Settle);
	}
}
