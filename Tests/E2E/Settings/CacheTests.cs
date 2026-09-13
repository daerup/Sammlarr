using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

[NotInParallel]
public class CacheTests
{
	private const string MetadataRequests = "/movie/";

	[Test]
	public async Task PurgeCache_WhenConfirmed_ForcesReResolution()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);
		await PreviewAsync(page);
		var warm = await E2EApp.RequestCountAsync(MetadataRequests);

		await PreviewAsync(page);

		await Assert.That(await E2EApp.RequestCountAsync(MetadataRequests)).IsEqualTo(warm);

		await SettingsPage.VisitSectionAsync(page, "cache");
		await PurgeAsync(page);
		await PreviewAsync(page);

		await Assert.That(await E2EApp.RequestCountAsync(MetadataRequests)).IsGreaterThan(warm);
	}

	private static async Task PreviewAsync(IPage page)
	{
		await page.VisitAsync(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, TestData.Name("Cache"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
	}

	private static async Task PurgeAsync(IPage page)
	{
		await page.Button("Purge cache").ClickAsync();
		await page.Button("Yes, purge everything").ClickAsync();
		await Expect(page.Text("Media cache purged.")).ToBeVisibleAsync();
	}
}
