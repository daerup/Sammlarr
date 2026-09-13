using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class SettingsPageTests
{
	[Test]
	public async Task Settings_WhenAuthenticatedAdmin_IsReachable()
	{
		var page = await E2EApp.AdminAt(Routes.Settings);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Assert.That(await page.TitleAsync()).Contains("Settings");
		await Expect(page.Heading("Settings")).ToBeVisibleAsync();
	}

	[Test]
	public async Task UsersSection_WhenAuthenticatedAdmin_IsRendered()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("users"));

		await Expect(page.Text("People with access to this Sammlarr instance.")).ToBeVisibleAsync();
	}
}
