using Microsoft.Playwright;

namespace E2E.Tests;

internal static class PageExtensions
{
	private const string HandlersAttached =
		"[...document.querySelectorAll('*')].some(element => [...element.attributes].some(attribute => attribute.name.startsWith('_bl_')))";

	private const string CircuitReady =
		$"() => !!window.Blazor && !document.querySelector('#components-reconnect-modal.components-reconnect-show') && {HandlersAttached}";

	internal static async Task VisitAsync(this IPage page, string route)
	{
		await page.GotoAsync(route);
		await page.WaitForFunctionAsync(CircuitReady, null, new PageWaitForFunctionOptions { Timeout = 30000 });
	}
}
