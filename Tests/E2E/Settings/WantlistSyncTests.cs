using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class WantlistSyncTests
{
	private const string ApiKey = "0123456789abcdef0123456789abcdef";

	[Test]
	public async Task RadarrAndSonarr_OnceBothAnswer_TakeOverTheWantlistUntilPlexIsPickedAgain()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));
		var wiremock = await E2EApp.WireMockUrlAsync();

		await page.Locator(".map-chip").ClickAsync();
		await page.MenuItem("Radarr + Sonarr").ClickAsync();
		var switchButton = page.ButtonContaining("Switch the wantlist to Radarr + Sonarr");
		await Expect(switchButton).ToBeDisabledAsync();
		await ConnectAsync(page, "Radarr", new Uri(wiremock, "radarr"));
		await ConnectAsync(page, "Sonarr", new Uri(wiremock, "sonarr"));
		await switchButton.ClickAsync();

		await Expect(page.Locator(".arr-row-head")).ToContainTextAsync("Connected");
		await Expect(page.Locator(".arr-row-head")).ToContainTextAsync("Radarr: Any into /movies");
		await Expect(SettingsPage.MapService(page, "Radarr + Sonarr")).ToContainTextAsync("Connected");

		await page.Locator(".map-chip").ClickAsync();
		await page.MenuItem("Plex").ClickAsync();

		await Expect(page.Locator("section[aria-label='Wantlist sync']")).ToHaveCountAsync(0);
		await Expect(SettingsPage.MapService(page, "Radarr + Sonarr")).ToHaveCountAsync(0);
	}

	[Test]
	public async Task StayOnPlex_DuringSetup_PutsTheWantlistSyncAway()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));

		await page.Locator(".map-chip").ClickAsync();
		await page.MenuItem("Radarr + Sonarr").ClickAsync();
		await page.Button("Stay on Plex").ClickAsync();

		await Expect(page.Locator("section[aria-label='Wantlist sync']")).ToHaveCountAsync(0);
		await Expect(SettingsPage.MapService(page, "Plex")).ToBeVisibleAsync();
	}

	private static async Task ConnectAsync(IPage page, string app, Uri url)
	{
		var section = page.Locator($"[aria-label='{app}']");
		await section.Locator($"input[aria-label='{app} URL']").EnterAsync(url.ToString());
		await section.Locator($"input[placeholder='From {app} › Settings › General']").EnterAsync(ApiKey);
		await section.Button("Test").ClickAsync();
		await Expect(section.Text("Connected")).ToBeVisibleAsync();
	}
}
