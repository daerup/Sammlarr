using System.Web;
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

	internal static async Task WaitForCircuitAsync(this IPage page)
	{
		try
		{
			await page.WaitForFunctionAsync(CircuitReady, null, new PageWaitForFunctionOptions { Timeout = Wait.Preview, PollingInterval = 100 });
		}
		catch (TimeoutException)
		{
			var blazor = await page.EvaluateAsync<bool>("() => !!window.Blazor");
			var reconnecting = await page.EvaluateAsync<bool>("() => !!document.querySelector('#components-reconnect-modal.components-reconnect-show')");
			var handlers = await page.EvaluateAsync<bool>($"() => {HandlersAttached}");
			var body = (await page.InnerTextAsync("body")).ReplaceLineEndings(" ");
			throw new TimeoutException(
				$"circuit never became ready at {page.Url} (blazor={blazor}, reconnecting={reconnecting}, handlers={handlers}), body '{body[..Math.Min(400, body.Length)]}'"
			);
		}
	}

	internal static async Task<string> PlexCallbackAsync(this IPage page, string returnUrl = "")
	{
		var login = returnUrl.Length == 0 ? "/auth/plex/login" : $"/auth/plex/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
		var response = await page.Context.APIRequest.GetAsync(login, new APIRequestContextOptions { MaxRedirects = 0 });
		var location = response.Headers["location"];
		var forward = HttpUtility.ParseQueryString(location[(location.IndexOf("#?", StringComparison.Ordinal) + 2)..])["forwardUrl"]!;
		return new Uri(forward).PathAndQuery;
	}

	internal static async Task ShouldBeAtAsync(this IPage page, string route, float timeout = Wait.Settle)
	{
		await page.WaitForURLAsync(url => new Uri(url).AbsolutePath == route, new PageWaitForURLOptions { Timeout = timeout });
	}

	internal static async Task ShouldNotBeAtAsync(this IPage page, string route) => await Assert.That(page.Url).DoesNotContain(route);
}
