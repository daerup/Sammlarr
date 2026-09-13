using System.Text.RegularExpressions;
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
		await page.WaitForCircuitAsync();
	}

	internal static Task WaitForCircuitAsync(this IPage page) =>
		page.WaitForFunctionAsync(CircuitReady, null, new PageWaitForFunctionOptions { Timeout = 30000 });

	internal static async Task ShouldBeAtAsync(this IPage page, string route, float timeout = Wait.Settle)
	{
		await page.WaitForURLAsync(new Regex(Regex.Escape(route)), new PageWaitForURLOptions { Timeout = timeout });
		await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo(route);
	}

	internal static async Task ShouldNotBeAtAsync(this IPage page, string route) => await Assert.That(page.Url).DoesNotContain(route);
}
