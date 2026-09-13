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
		var page = await E2EApp.OpenAuthenticatedPageAsync();
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
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, $"E2E Cache {Guid.NewGuid().ToString("N")[..6]}");
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
	}

	private static async Task PurgeAsync(IPage page)
	{
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Purge cache", Exact = true }).ClickAsync();
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Yes, purge everything", Exact = true }).ClickAsync();
		await Expect(page.GetByText("Media cache purged.")).ToBeVisibleAsync();
	}
}
