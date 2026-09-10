using Microsoft.Playwright;

namespace E2E.Tests;

internal static class PageExtensions
{
	internal static async Task VisitAsync(this IPage page, string route)
	{
		await page.GotoAsync(route);
		if (E2EApp.IsRecording)
		{
			await E2EApp.CaptureAsync(page);
		}
	}
}
