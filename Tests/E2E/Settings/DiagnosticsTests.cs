using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

[NotInParallel]
public class DiagnosticsTests
{
	private const string FilterInput = "input[placeholder='Filter by message, category, or trace id…']";

	[Test]
	public async Task LogStream_WhenOpened_ShowsEntries()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await CaptureEntriesAsync(page);

		await Expect(page.GetByText("Waiting for log entries…")).ToBeHiddenAsync();
		await Expect(Button(page, "Export")).ToBeEnabledAsync();
		await StopAsync(page);
	}

	[Test]
	public async Task Filter_WhenNoMatch_ShowsEmptyMessage()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await CaptureEntriesAsync(page);

		await page.Locator(FilterInput).FillAsync(Guid.NewGuid().ToString("N"));

		await Expect(page.GetByText("No entries match the filter.")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
		await StopAsync(page);
	}

	[Test]
	public async Task Export_WhenTriggeredAsAdmin_DownloadsFile()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await CaptureEntriesAsync(page);

		var download = await page.RunAndWaitForDownloadAsync(() => Button(page, "Export").ClickAsync());

		await Assert.That(download.SuggestedFilename).StartsWith("diagnostics-");
		await Assert.That(download.SuggestedFilename).EndsWith(".zip");
		await StopAsync(page);
	}

	private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });

	private static async Task CaptureEntriesAsync(IPage page)
	{
		await SettingsPage.VisitSectionAsync(page, "diagnostics");
		await Button(page, "Start").ClickAsync();

		var noise = await E2EApp.OpenAuthenticatedPageAsync();
		await noise.VisitAsync("/feeds");

		await Expect(page.GetByText("Waiting for log entries…")).ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions { Timeout = 30000 });
	}

	private static async Task StopAsync(IPage page)
	{
		await Button(page, "Stop").ClickAsync();
		await Expect(Button(page, "Start")).ToBeVisibleAsync();
	}
}
