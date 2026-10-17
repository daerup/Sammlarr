using Microsoft.Playwright;

namespace E2E.Tests;

internal static class BrowserPages
{
	private const string PlaceholderArtwork = """
		<svg xmlns="http://www.w3.org/2000/svg" width="200" height="300" viewBox="0 0 200 300">
		  <rect width="200" height="300" fill="#334155"/>
		  <g fill="none" stroke="#94a3b8" stroke-width="5" stroke-linejoin="round" stroke-linecap="round">
		    <rect x="66" y="98" width="68" height="52" rx="6"/>
		    <circle cx="84" cy="114" r="6"/>
		    <path d="M66 142 l22 -18 l16 13 l10 -8 l20 17"/>
		  </g>
		  <text x="100" y="186" text-anchor="middle" font-family="system-ui, sans-serif" font-size="26" font-weight="700" fill="#e2e8f0">E2E</text>
		  <text x="100" y="203" text-anchor="middle" font-family="system-ui, sans-serif" font-size="12" letter-spacing="1.5" fill="#94a3b8">PLACEHOLDER</text>
		</svg>
		""";

	internal static Task<IBrowser> LaunchAsync(IPlaywright playwright)
	{
		var headed = Environment.GetEnvironmentVariable("E2E_HEADED") is "true";
		Assertions.SetDefaultExpectTimeout(Wait.Default);
		return playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = !headed, SlowMo = headed ? 2000 : 0 });
	}

	internal static async Task<IBrowserContext> OpenAsync(IBrowser browser, string baseUrl)
	{
		var context = await browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
		await context.RouteAsync(url => !url.StartsWith(baseUrl, StringComparison.Ordinal), ServeArtworkLocallyAsync);
		await context.AddInitScriptAsync(Locators.MarkListeningInputs);
		return context;
	}

	private static Task ServeArtworkLocallyAsync(IRoute route)
	{
		return route.Request.ResourceType == "image"
			? route.FulfillAsync(new RouteFulfillOptions { ContentType = "image/svg+xml", Body = PlaceholderArtwork })
			: route.ContinueAsync();
	}
}
