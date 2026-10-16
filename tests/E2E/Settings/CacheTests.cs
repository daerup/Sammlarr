using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class CacheTests
{
	private const string DetailsRequests = "/movie/";

	[Test]
	public async Task ClearTitleIds_WhenConfirmed_IdentifiesTheTitlesAgain()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);
		await PreviewAsync(page);
		var warm = await E2EApp.RequestCountAsync(DetailsRequests);

		await PreviewAsync(page);

		await Assert.That(await E2EApp.RequestCountAsync(DetailsRequests)).IsEqualTo(warm);

		await SettingsPage.VisitSectionAsync(page, "housekeeping");
		await ClearIdsAsync(page);
		await PreviewAsync(page);

		await Assert.That(await E2EApp.RequestCountAsync(DetailsRequests)).IsGreaterThan(warm);
	}

	private static async Task PreviewAsync(IPage page)
	{
		await page.VisitAsync(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, TestData.Name("Cache"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
	}

	private static async Task ClearIdsAsync(IPage page)
	{
		await page.GetByTestId("housekeeping-clear-ids").Button("Clear…").ClickAsync();
		await page.Button("Clear title ids and details").ClickAsync();
		await Expect(page.Text("Title ids and details cleared.")).ToBeVisibleAsync();
	}
}
