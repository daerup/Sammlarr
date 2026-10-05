using System.Text.RegularExpressions;
using E2E.Tests.Settings;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

internal static class SelfHostedWantlist
{
	internal static ILocator Tile(IPage page, string title)
	{
		return page.Locator(".media-card")
			.Filter(new LocatorFilterOptions { Has = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true }) });
	}

	internal static async Task AddAsync(IPage page, string title)
	{
		var row = await SearchAsync(page, title);
		await row.Locator("button[aria-label='Add to wantlist']").ClickAsync();
		await Expect(row.Locator("button[aria-label='Remove from wantlist']")).ToBeVisibleAsync();
		await page.Keyboard.PressAsync("Escape");
	}

	internal static async Task RemoveAsync(IPage page, string title)
	{
		var row = await SearchAsync(page, title);
		await row.Locator("button[aria-label='Remove from wantlist']").ClickAsync();
		await Expect(row.Locator("button[aria-label='Add to wantlist']")).ToBeVisibleAsync();
		await page.Keyboard.PressAsync("Escape");
	}

	internal static async Task<ILocator> OpenSyncAsync(IPage page)
	{
		await page.VisitAsync(Routes.Wantlist);
		await SyncGroup(page).Button("Sync").ClickAsync();
		var dialog = page.GetByRole(AriaRole.Dialog).Filter(new LocatorFilterOptions { HasText = "Review changes before syncing" });
		await Expect(dialog).ToBeVisibleAsync();
		return dialog;
	}

	internal static async Task SyncAsync(IPage page)
	{
		var dialog = await OpenSyncAsync(page);
		var syncNow = dialog.ButtonContaining("Sync now");
		await Expect(syncNow).ToBeEnabledAsync(Wait.Settle);
		await syncNow.ClickAsync();
		await Expect(page.Text("Sync has been queued")).ToBeVisibleAsync();
		await Expect(SyncGroup(page)).ToContainTextAsync("In sync", new LocatorAssertionsToContainTextOptions { Timeout = Wait.Preview });
	}

	internal static async Task SearchWithRadarrAndSonarrAsync(IPage page)
	{
		await SettingsPage.PickAsync(page, "Search", "Radarr + Sonarr");
		await Expect(page.GetByText("Search now uses Radarr + Sonarr.")).ToBeVisibleAsync();
	}

	private static ILocator SyncGroup(IPage page) => page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "Wantlist sync" });

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
