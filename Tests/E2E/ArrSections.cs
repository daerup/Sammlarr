using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests;

internal static class ArrSections
{
	internal static async Task<ILocator> OpenAsync(IPage page, string app)
	{
		await Tab(page, app).ClickAsync();
		return page.Locator($"[data-arr-app='{app}']");
	}

	internal static async Task ConnectAsync(IPage page, string app, Uri url)
	{
		var section = await OpenAsync(page, app);
		await section.Locator($"input[aria-label='{app} URL']").EnterAsync(url.ToString());
		await section.Locator("input[placeholder='Enter API key']").EnterAsync(StackBoot.ArrApiKey);
		await section.Button("Test").ClickAsync();
		await Expect(Tab(page, app)).ToContainTextAsync("Connected");
	}

	private static ILocator Tab(IPage page, string app) => page.GetByRole(AriaRole.Tab).Filter(new LocatorFilterOptions { HasText = app });
}
