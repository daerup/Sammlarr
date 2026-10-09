using Microsoft.Playwright;

namespace E2E.Tests.Settings;

internal static class SettingsPage
{
	internal static string Section(string slug) => $"{Routes.Settings}?section={slug}";

	internal static Task VisitSectionAsync(IPage page, string slug) => page.VisitAsync(Section(slug));

	internal static ILocator MapService(IPage page, string name) => page.Locator(".map-service").Filter(new LocatorFilterOptions { HasText = name });

	internal static ILocator MapSource(IPage page, string name) => page.Locator(".map-source").Filter(new LocatorFilterOptions { HasText = name });

	internal static ILocator MapChip(IPage page, string role) => page.Locator($".map-chip[aria-label='Change what {role} uses']");

	internal static async Task PickAsync(IPage page, string role, string option)
	{
		await VisitSectionAsync(page, "providers");
		await MapChip(page, role).ClickAsync();
		await page.MenuItem(option).ClickAsync();
	}

	internal static async Task<ILocator> OpenWantlistSyncAsync(IPage page)
	{
		await VisitSectionAsync(page, "providers");
		var row = page.Locator("#provider-row-arr");
		await row.Locator("button").First.ClickAsync();
		return row;
	}

	internal static ILocator PersonRow(IPage page, string user) => page.Locator($"[data-person='{user}']");

	internal static async Task OpenPersonAsync(IPage page, string user)
	{
		var trigger = PersonRow(page, user).Locator("button").First;
		if (await trigger.GetAttributeAsync("data-state") != "open")
		{
			await trigger.ClickAsync();
		}
	}

	internal static async Task<ILocator> OpenProviderAsync(IPage page, string provider)
	{
		var row = page.Locator("div.provider-row").Filter(new LocatorFilterOptions { Has = page.ButtonContaining(provider) });
		await row.ButtonContaining(provider).ClickAsync();
		return row;
	}
}
