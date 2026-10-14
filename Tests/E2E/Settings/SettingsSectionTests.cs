using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class SettingsSectionTests
{
	[Test]
	public async Task About_WhenSelectedByQueryAsAdmin_ShowsTheTmdbNotice()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("about"));

		await Expect(page.GetByText("This product uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Section_WhenSlugIsUnknown_FallsBackToTheSettingsPageAndItsTitle()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("does-not-exist"));

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Assert.That(await page.TitleAsync()).Contains("Settings");
		await Expect(page.Heading("Settings")).ToBeVisibleAsync();
	}
}
