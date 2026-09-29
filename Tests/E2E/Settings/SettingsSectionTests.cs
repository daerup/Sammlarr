using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class SettingsSectionTests
{
	[Test]
	[Arguments("providers", "Which service does what, and the account, keys and connections behind it.")]
	[Arguments("notifications", "Webhook or Pushover connections to deliver to.")]
	[Arguments("cache", "Id groups.")]
	[Arguments("diagnostics", "Live log stream")]
	[Arguments("users", "People with access to this Sammlarr instance.")]
	[Arguments("testing", "Fire a toast of the chosen kind")]
	public async Task Section_WhenSelectedByQueryAsAdmin_RendersThatSection(string slug, string expectedContent)
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section(slug));

		await Expect(page.GetByText(expectedContent)).ToBeVisibleAsync();
	}

	[Test]
	[Arguments("Cache")]
	[Arguments("Diagnostics")]
	[Arguments("Users")]
	[Arguments("Testing")]
	public async Task AdminSection_WhenSignedInAsAdmin_IsOfferedInTheSectionNav(string label)
	{
		var page = await E2EApp.AdminAt(Routes.Settings);

		await Expect(page.Button(label)).ToBeVisibleAsync();
	}

	[Test]
	public async Task Section_WhenSlugIsUnknown_FallsBackWithoutFailing()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("does-not-exist"));

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Expect(page.Heading("Settings")).ToBeVisibleAsync();
	}
}
