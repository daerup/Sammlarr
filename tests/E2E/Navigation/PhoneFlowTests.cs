using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Navigation;

[Devices(Device.Phone)]
public class PhoneFlowTests(Device device) : E2ETests(device)
{
	[Test]
	public async Task Menu_OpensAndNavigatesToASettingsSection()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await page.Button("Open menu").ClickAsync();
		await page.Modal("Menu").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Notifications", Exact = true }).ClickAsync();

		await page.ShouldBeAtAsync(Routes.Settings);
		await Expect(page.Modal("Menu")).ToBeHiddenAsync();
		await Expect(page.Locator("header .phone-title")).ToHaveTextAsync("Notifications");
	}
}
