using System.Text.RegularExpressions;
using E2E.Tests.Settings;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Common;

public class UiInvariantTests
{
	[Test]
	public async Task CtrlK_WhenPressed_OpensSearch()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/blacklist");
		await Expect(page.GetByText("Add from Search")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
		await Expect(page.GetByText("Search & Add to Blacklist")).ToBeHiddenAsync();

		var search = page.GetByText("Search & Add to Blacklist");
		for (var attempt = 0; attempt < 10 && !await search.IsVisibleAsync(); attempt++)
		{
			await page.Keyboard.PressAsync("Control+k");
			await page.WaitForTimeoutAsync(1000);
		}

		await Expect(search).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
	}

	[Test]
	public async Task TestingSection_WhenExceptionThrown_OpensErrorDialog()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "testing");

		var toasts = page.Locator("section").Filter(new LocatorFilterOptions { HasText = "Fire a toast of the chosen kind" });
		await PressAsync(toasts.GetByRole(AriaRole.Combobox));
		await PressAsync(page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = "Exception", Exact = true }));
		await PressAsync(toasts.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Show", Exact = true }));
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "View details", Exact = true }).ClickAsync();

		await Expect(page.Locator(".error-dialog")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
		await Expect(page.GetByText("System.InvalidOperationException")).ToBeVisibleAsync();
		await Expect(page.GetByText("Stack Trace")).ToBeVisibleAsync();
	}

	[Test]
	public async Task ThemeToggle_WhenFlipped_PersistsAcrossNavigation()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds");
		await Expect(page.Locator("html")).Not.ToHaveClassAsync(new Regex("dark"));

		await page.GetByLabel("Switch to dark mode").ClickAsync();

		await Expect(page.Locator("html")).ToHaveClassAsync(new Regex("dark"), new LocatorAssertionsToHaveClassOptions { Timeout = 30000 });
		await Expect(page.GetByLabel("Switch to light mode")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });

		await page.VisitAsync("/browse");

		await Expect(page.Locator("html")).ToHaveClassAsync(new Regex("dark"), new LocatorAssertionsToHaveClassOptions { Timeout = 30000 });
		await Expect(page.GetByLabel("Switch to light mode")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
	}

	private static Task PressAsync(ILocator locator) => locator.DispatchEventAsync("click");
}
