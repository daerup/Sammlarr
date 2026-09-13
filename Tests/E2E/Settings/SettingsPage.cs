using Microsoft.Playwright;

namespace E2E.Tests.Settings;

internal static class SettingsPage
{
	internal static string Section(string slug) => $"{Routes.Settings}?section={slug}";

	internal static Task VisitSectionAsync(IPage page, string slug) => page.VisitAsync(Section(slug));

	internal static ILocator Card(IPage page, string title) => page.Locator("div.max-w-lg").Filter(new LocatorFilterOptions { Has = page.Heading(title) });
}
