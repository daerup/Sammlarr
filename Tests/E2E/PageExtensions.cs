using Microsoft.Playwright;

namespace E2E.Tests;

internal static class PageExtensions
{
	private const string CircuitReady = "() => !!window.Blazor && !document.querySelector('#components-reconnect-modal.components-reconnect-show')";

	internal static async Task VisitAsync(this IPage page, string route)
	{
		await page.GotoAsync(route);
		await page.WaitForFunctionAsync(CircuitReady, null, new PageWaitForFunctionOptions { Timeout = 30000 });
	}
}
