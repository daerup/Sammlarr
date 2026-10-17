using Microsoft.Playwright;

namespace E2E.Tests.Browse;

internal static class TrendingTiles
{
	internal const string Title = "Moana";

	internal static ILocator Resolved(IPage page, string title) =>
		page.Locator("[data-testid='media-tile']:not(.tile-unmatched)").Filter(new LocatorFilterOptions { Has = page.Heading(title) });
}
