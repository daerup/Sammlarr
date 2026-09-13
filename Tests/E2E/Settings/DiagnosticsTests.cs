using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

[NotInParallel]
public class DiagnosticsTests
{
	private const string FilterInput = "input[placeholder='Filter by message, category, or trace id…']";

	private const string Waiting = "Waiting for log entries…";

	[Test]
	public async Task LogStream_WhenOpened_ShowsEntries()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await CaptureEntriesAsync(page);

		await Expect(page.Text(Waiting)).ToBeHiddenAsync();
		await Expect(page.Button("Export")).ToBeEnabledAsync();
		await StopAsync(page);
	}

	[Test]
	public async Task Filter_WhenNoMatch_ShowsEmptyMessage()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await CaptureEntriesAsync(page);

		await page.Locator(FilterInput).FillAsync(Guid.NewGuid().ToString("N"));

		await Expect(page.Text("No entries match the filter.")).ToBeVisibleAsync(Wait.Settle);
		await StopAsync(page);
	}

	[Test]
	public async Task Export_WhenTriggeredAsAdmin_DownloadsFile()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await CaptureEntriesAsync(page);

		var download = await page.RunAndWaitForDownloadAsync(() => page.Button("Export").ClickAsync());

		await Assert.That(download.SuggestedFilename).StartsWith("diagnostics-");
		await Assert.That(download.SuggestedFilename).EndsWith(".zip");
		await StopAsync(page);
	}

	private static async Task CaptureEntriesAsync(IPage page)
	{
		await SettingsPage.VisitSectionAsync(page, "diagnostics");
		await page.Button("Start").ClickAsync();

		await E2EApp.AdminAt(Routes.Feeds);

		await Expect(page.Text(Waiting)).ToBeHiddenAsync(Wait.Settle);
	}

	private static async Task StopAsync(IPage page)
	{
		await page.Button("Stop").ClickAsync();
		await Expect(page.Button("Start")).ToBeVisibleAsync();
	}
}
