using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests;

internal static class ArrSections
{
	internal static async Task ConnectAsync(IPage page, string app, Uri url)
	{
		var section = page.Locator($"[aria-label='{app}']");
		await section.Locator($"input[aria-label='{app} URL']").EnterAsync(url.ToString());
		await section.Locator("input[placeholder='Enter API key']").EnterAsync(StackBoot.ArrApiKey);
		await section.Button("Test").ClickAsync();
		await Expect(section.Text("Connected")).ToBeVisibleAsync();
	}
}
