using System.Web;
using Microsoft.Playwright;

namespace E2E.Tests;

internal static class PageExtensions
{
	private const string HandlersAttached =
		"[...document.querySelectorAll('*')].some(element => [...element.attributes].some(attribute => attribute.name.startsWith('_bl_')))";

	private const string CircuitReady =
		$"() => !!window.Blazor && !document.querySelector('#components-reconnect-modal.components-reconnect-show') && {HandlersAttached}";

	private const string Overflowing = """
		async () => {
			await document.fonts.ready;
			const width = document.documentElement.clientWidth;
			const limit = width + 1;
			const path = element => {
				const parts = [];
				for (let node = element; node && node !== document.body && parts.length < 4; node = node.parentElement) {
					const classes = typeof node.className === 'string' ? node.className.trim().split(/\s+/).filter(Boolean).slice(0, 2) : [];
					parts.unshift(node.tagName.toLowerCase() + (node.id ? `#${node.id}` : '') + classes.map(name => `.${name}`).join(''));
				}
				return parts.join(' > ');
			};
			const clipped = node => {
				for (let child = node, parent = node.parentElement; parent && parent !== document.documentElement; child = parent, parent = parent.parentElement) {
					if (child instanceof Element && getComputedStyle(child).position === 'fixed') return false;
					const box = parent.getBoundingClientRect();
					const fullWidth = box.left <= 0.5 && box.right >= width - 0.5;
					if (box.right <= limit && !fullWidth && /(hidden|clip)/.test(getComputedStyle(parent).overflowX)) return true;
				}
				return false;
			};
			const shown = (node, element, box) => box.right > limit && box.width > 0 && getComputedStyle(element).visibility !== 'hidden' && !clipped(node);
			const overflowing = new Set([...document.querySelectorAll('body *')].filter(element => shown(element, element, element.getBoundingClientRect())));
			const texts = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
			const range = document.createRange();
			for (let text = texts.nextNode(); text; text = texts.nextNode()) {
				range.selectNodeContents(text);
				if (text.textContent.trim() && shown(text, text.parentElement, range.getBoundingClientRect())) overflowing.add(text.parentElement);
			}
			return [...overflowing].filter(element => !overflowing.has(element.parentElement)).map(path);
		}
		""";

	internal static Task<string[]> OverflowingAsync(this IPage page) => page.EvaluateAsync<string[]>(Overflowing);

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
		catch (TimeoutException exception)
		{
			var blazor = await page.EvaluateAsync<bool>("() => !!window.Blazor");
			var reconnecting = await page.EvaluateAsync<bool>("() => !!document.querySelector('#components-reconnect-modal.components-reconnect-show')");
			var handlers = await page.EvaluateAsync<bool>($"() => {HandlersAttached}");
			var body = (await page.InnerTextAsync("body")).ReplaceLineEndings(" ");
			throw new TimeoutException(
				$"circuit never became ready at {page.Url} (blazor={blazor}, reconnecting={reconnecting}, handlers={handlers}), body '{body[..Math.Min(400, body.Length)]}'",
				exception
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
