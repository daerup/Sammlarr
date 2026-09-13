namespace E2E.Tests.Navigation;

public class ShellNavigationTests
{
	[Test]
	[Arguments("Browse", Routes.Browse)]
	[Arguments("Watchlist", Routes.Watchlist)]
	[Arguments("Blacklist", Routes.Blacklist)]
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

		await page.MenuItem("Watchlist").First.ClickAsync();
		await page.ShouldBeAtAsync(Routes.Watchlist);

		await page.MenuItem("Blacklist").First.ClickAsync();
		await page.ShouldBeAtAsync(Routes.Blacklist);

		await page.MenuItem("Feeds").First.ClickAsync();

		await page.ShouldBeAtAsync(Routes.Feeds);
		await page.ShouldNotBeAtAsync(Routes.SignIn);
	}
}
