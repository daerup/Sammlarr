using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class DiagnosticsTests
{
	private const string FilterInput = "input[placeholder='Filter by message, category, or trace id…']";

	[Test]
	public async Task Filter_WhenNoMatch_ShowsEmptyMessage()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await CaptureEntriesAsync(page);

		await page.Locator(FilterInput).EnterAsync(Guid.NewGuid().ToString("N"));

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
		var unknownList = $"e2e/diagnostics-{Guid.NewGuid():N}";
		await using var stub = await E2EApp.StubAsync($"/lists/{unknownList}/items", 404, "{}");
		await SettingsPage.VisitSectionAsync(page, "diagnostics");
		await page.Button("Start").ClickAsync();

		var editor = await E2EApp.AdminAt(Routes.CreateFeed);
		await FeedEditor.SelectMdblistAsync(editor);
		await FeedEditor.FillAsync(editor, "#list-identifier", unknownList);
		await FeedEditor.LoadAsync(editor);

		await Expect(page.GetByText(unknownList)).ToBeVisibleAsync(Wait.Preview);
	}

	private static async Task StopAsync(IPage page)
	{
		await page.Button("Stop").ClickAsync();
		await Expect(page.Button("Start")).ToBeVisibleAsync();
	}
}
