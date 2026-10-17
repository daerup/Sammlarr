using Microsoft.Playwright;

namespace E2E.Tests;

internal static class FailureSnapshot
{
	private const int BodyLength = 1500;

	internal static async Task WriteAsync(TestContext context, IEnumerable<IBrowserContext> browsers)
	{
		if (context.Execution.Result?.State is not (TestState.Failed or TestState.Timeout))
		{
			return;
		}

		foreach (var page in browsers.SelectMany(browser => browser.Pages).Where(page => !page.IsClosed))
		{
			var body = (await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5000 })).ReplaceLineEndings(" ");
			context.Output.WriteLine($"{page.Url} showed '{body[..Math.Min(BodyLength, body.Length)]}'");
		}
	}
}
