using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

internal static class SelfHostedWantlist
{
	internal static async Task AddAsync(IPage page, string title)
	{
		var row = await SearchAsync(page, title);
		await row.Locator("button[aria-label='Add to wantlist']").ClickAsync();
		await Expect(row.Locator("button[aria-label='Remove from wantlist']")).ToBeVisibleAsync();
		await page.Keyboard.PressAsync("Escape");
	}

	internal static async Task SyncAsync(IPage page)
	{
		await page.VisitAsync(Routes.Wantlist);
		await page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "Wantlist sync" }).Button("Sync").ClickAsync();
		var syncNow = page.ButtonContaining("Sync now");
		await Expect(syncNow).ToBeEnabledAsync(Wait.Settle);
		await syncNow.ClickAsync();
		await Expect(page.Text("Sync has been queued")).ToBeVisibleAsync();
	}

	private static async Task<ILocator> SearchAsync(IPage page, string title)
	{
		await page.Locator("button[aria-label='Search for a movie or show']").ClickAsync();
		var modal = page.Locator(".search-modal");
		var input = modal.Locator("#search-input");
		await input.FillAsync(title);
		await input.PressAsync("Enter");
		var named = page.Locator(".media-title-text", new PageLocatorOptions { HasTextRegex = new Regex($"^{Regex.Escape(title)}$") });
		var row = modal.Locator(".search-result-row").Filter(new LocatorFilterOptions { Has = named }).First;
		await Expect(row).ToBeVisibleAsync(Wait.Settle);
		return row;
	}
}
