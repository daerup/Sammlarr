using Microsoft.Playwright;

namespace E2E.Tests.Settings;

internal static class SettingsPage
{
	internal static string Section(string slug) => $"{Routes.Settings}?section={slug}";

	internal static Task VisitSectionAsync(IPage page, string slug) => page.VisitAsync(Section(slug));

	internal static async Task<ILocator> OpenProviderAsync(IPage page, string provider)
	{
		var row = page.Locator("div.provider-row").Filter(new LocatorFilterOptions { Has = page.ButtonContaining(provider) });
		await row.ButtonContaining(provider).ClickAsync();
		return row;
	}
}
