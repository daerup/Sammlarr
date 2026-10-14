using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class ServicesMapTests
{
	[Test]
	public async Task Map_ForAnAdmin_IsAsWideAsTheAccountsAndKeysCard()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));
		await Expect(SettingsPage.MapService(page, "Plex")).ToBeVisibleAsync();

		var accounts = await page.Locator("section[aria-label='Accounts and keys'] > div").BoundingBoxAsync();
		var map = await page.Locator("section[aria-label='Services'] > div").BoundingBoxAsync();

		await Assert.That(map!.Width).IsEqualTo(accounts!.Width);
	}

	[Test]
	public async Task Map_WhenTmdbIsClicked_OpensTmdbsRowAndScrollsItIntoView()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));

		await page.ButtonContaining("Open TMDb").ClickAsync();

		var row = page.Locator("#provider-row-tmdb");
		await Expect(row).ToBeInViewportAsync();
		await Expect(row.Text("API key")).ToBeVisibleAsync();
	}
}
