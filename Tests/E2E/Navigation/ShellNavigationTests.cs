namespace E2E.Tests.Navigation;

public class ShellNavigationTests
{
	[Test]
	[Arguments("Browse", Routes.Browse)]
	[Arguments("Wantlist", Routes.Wantlist)]
	[Arguments("Blocklist", Routes.Blocklist)]
	[Arguments("Feeds", Routes.Feeds)]
	public async Task NavMenuItem_WhenClickedWhileAuthenticated_NavigatesToItsPage(string label, string route)
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await page.MenuItem(label).First.ClickAsync();

		await page.ShouldBeAtAsync(route);
		await page.ShouldNotBeAtAsync(Routes.SignIn);
	}

	[Test]
	public async Task SettingsLink_WhenClickedWhileAuthenticated_NavigatesToSettings()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await page.Link("Settings").First.ClickAsync();

		await page.ShouldBeAtAsync(Routes.Settings);
	}

	[Test]
	public async Task Shell_WhenNavigatingBetweenPages_KeepsTheSessionAlive()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await page.MenuItem("Wantlist").First.ClickAsync();
		await page.ShouldBeAtAsync(Routes.Wantlist);

		await page.MenuItem("Blocklist").First.ClickAsync();
		await page.ShouldBeAtAsync(Routes.Blocklist);

		await page.MenuItem("Feeds").First.ClickAsync();

		await page.ShouldBeAtAsync(Routes.Feeds);
		await page.ShouldNotBeAtAsync(Routes.SignIn);
	}
}
