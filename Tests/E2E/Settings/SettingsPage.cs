using Microsoft.Playwright;

namespace E2E.Tests.Settings;

internal static class SettingsPage
{
	internal static Task VisitSectionAsync(IPage page, string slug) => page.VisitAsync($"/settings?section={slug}");

	internal static ILocator Card(IPage page, string title) =>
		page.Locator("div.max-w-lg")
			.Filter(new LocatorFilterOptions { Has = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true }) });

	internal static ILocator Button(ILocator scope, string name) => scope.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true });
}
