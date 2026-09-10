using Microsoft.Playwright;

namespace E2E.Tests.Settings;

public class SettingsPageTests
{
	[Test]
	public async Task Settings_WhenAuthenticatedAdmin_IsReachable()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/settings");
		var heading = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Settings" });
		await heading.WaitForAsync();

		await Assert.That(page.Url).DoesNotContain("/signin");
		await Assert.That(await page.TitleAsync()).Contains("Settings");
		await Assert.That(await heading.IsVisibleAsync()).IsTrue();
	}

	[Test]
	public async Task UsersSection_WhenAuthenticatedAdmin_IsRendered()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/settings?section=users");
		var usersSection = page.GetByText("People with access to this Sammlarr instance.");
		await usersSection.WaitForAsync();

		await Assert.That(await usersSection.IsVisibleAsync()).IsTrue();
	}
}
