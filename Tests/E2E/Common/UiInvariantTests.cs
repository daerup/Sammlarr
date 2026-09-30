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
		var page = await E2EApp.AdminAt(Routes.Blacklist);
		await Expect(page.Heading("Blacklist")).ToBeVisibleAsync(Wait.Settle);
		var search = page.Modal("Search");
		await Expect(search).ToBeHiddenAsync();

		for (var attempt = 0; attempt < 10 && !await search.IsVisibleAsync(); attempt++)
		{
			await page.Keyboard.PressAsync("Control+k");
			await page.WaitForTimeoutAsync(1000);
		}

		await Expect(search).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task TestingSection_WhenExceptionThrown_OpensErrorDialog()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("testing"));

		var toasts = page.Locator("section").Filter(new LocatorFilterOptions { HasText = "Fire a toast of the chosen kind" });
		await PressAsync(toasts.Combobox);
		await PressAsync(page.Option("Exception"));
		await PressAsync(toasts.Button("Show"));
		await page.Button("View details").ClickAsync();

		await Expect(page.Locator(".error-dialog")).ToBeVisibleAsync(Wait.Settle);
		await Expect(page.GetByText("System.InvalidOperationException")).ToBeVisibleAsync();
		await Expect(page.GetByText("Stack Trace")).ToBeVisibleAsync();
	}

	[Test]
	public async Task ThemeToggle_WhenFlipped_PersistsAcrossNavigation()
	{
		var page = await E2EApp.AdminAt(Routes.Feeds);
		await Expect(page.Locator("html")).Not.ToHaveClassAsync(new Regex("dark"));

		await page.GetByLabel("Switch to dark mode").ClickAsync();

		await ShouldBeDarkAsync(page);

		await page.VisitAsync(Routes.Browse);

		await ShouldBeDarkAsync(page);
	}

	private static async Task ShouldBeDarkAsync(IPage page)
	{
		await Expect(page.Locator("html")).ToHaveClassAsync(new Regex("dark"), new LocatorAssertionsToHaveClassOptions { Timeout = Wait.Settle });
		await Expect(page.GetByLabel("Switch to light mode")).ToBeVisibleAsync(Wait.Settle);
	}

	private static Task PressAsync(ILocator locator) => locator.DispatchEventAsync("click");
}
