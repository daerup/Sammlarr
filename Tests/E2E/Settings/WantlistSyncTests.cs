using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class WantlistSyncTests
{
	[Test]
	public async Task RadarrAndSonarr_OnceBothAnswer_TakeOverTheWantlistUntilPlexIsPickedAgain()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));
		var wiremock = await E2EApp.WireMockUrlAsync();

		await SettingsPage.MapChip(page, "Wantlist").ClickAsync();
		await page.MenuItem("Radarr + Sonarr").ClickAsync();
		var switchButton = page.ButtonContaining("Switch the wantlist to Radarr + Sonarr");
		await Expect(switchButton).ToBeDisabledAsync();
		await ArrSections.ConnectAsync(page, "Radarr", new Uri(wiremock, "radarr"));
		await ArrSections.ConnectAsync(page, "Sonarr", new Uri(wiremock, "sonarr"));
		await switchButton.ClickAsync();
		await page.Button("Switch to Radarr + Sonarr").ClickAsync();

		await Expect(page.Locator(".arr-row-head")).ToContainTextAsync("Connected");
		await Expect(SettingsPage.MapService(page, "Radarr + Sonarr")).ToContainTextAsync("Connected");

		await SettingsPage.MapChip(page, "Wantlist").ClickAsync();
		await page.MenuItem("Plex").ClickAsync();
		await page.Button("Switch to Plex").ClickAsync();

		await Expect(page.Locator(".arr-row")).ToHaveCountAsync(0);
		await Expect(SettingsPage.MapService(page, "Radarr + Sonarr")).ToHaveCountAsync(0);
	}

	[Test]
	public async Task StayOnPlex_DuringSetup_PutsTheWantlistSyncAway()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));

		await SettingsPage.MapChip(page, "Wantlist").ClickAsync();
		await page.MenuItem("Radarr + Sonarr").ClickAsync();
		await page.Button("Stay on Plex").ClickAsync();

		await Expect(page.Locator(".arr-row")).ToHaveCountAsync(0);
		await Expect(SettingsPage.MapService(page, "Plex")).ToBeVisibleAsync();
	}
}
