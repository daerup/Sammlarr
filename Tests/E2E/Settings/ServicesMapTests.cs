using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class ServicesMapTests
{
	[Test]
	public async Task Map_ForAMember_ShowsWhoManagesItAndOffersNoPicker()
	{
		var page = await E2EApp.MemberAt(SettingsPage.Section("providers"));

		await Expect(SettingsPage.MapService(page, "Plex")).ToBeVisibleAsync();
		await Expect(page.Text("Managed by an admin")).ToBeVisibleAsync();
		await Expect(page.Locator("button.map-service")).ToHaveCountAsync(0);
	}

	[Test]
	public async Task Map_ForAnAdmin_OpensTheWatchlistTargetFromThePlexBox()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));

		await page.Locator("button.map-service").Filter(new() { HasText = "Plex" }).ClickAsync();

		await Expect(page.Text("Watchlist syncs to")).ToBeVisibleAsync();
		await Expect(page.MenuItem("Plex")).ToBeVisibleAsync();
	}
}
