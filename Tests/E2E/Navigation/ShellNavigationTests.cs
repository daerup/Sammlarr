using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace E2E.Tests.Navigation;

public class ShellNavigationTests
{
	[Test]
	[Arguments("Browse", "/browse")]
	[Arguments("Watchlist", "/watchlist")]
	[Arguments("Blacklist", "/blacklist")]
	[Arguments("Feeds", "/feeds")]
	public async Task NavMenuItem_WhenClickedWhileAuthenticated_NavigatesToItsPage(string label, string route)
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/");

		await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = label }).First.ClickAsync();
		await page.WaitForURLAsync(new Regex(Regex.Escape(route)));

		await Assert.That(page.Url).Contains(route);
		await Assert.That(page.Url).DoesNotContain("/signin");
	}

	[Test]
	public async Task SettingsLink_WhenClickedWhileAuthenticated_NavigatesToSettings()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/");

		await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Settings" }).First.ClickAsync();
		await page.WaitForURLAsync(new Regex("/settings"));

		await Assert.That(page.Url).Contains("/settings");
	}

	[Test]
	public async Task Shell_WhenNavigatingBetweenPages_KeepsTheSessionAlive()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/watchlist");
		await page.VisitAsync("/blacklist");
		await page.VisitAsync("/feeds");

		await Assert.That(page.Url).Contains("/feeds");
		await Assert.That(page.Url).DoesNotContain("/signin");
	}
}
