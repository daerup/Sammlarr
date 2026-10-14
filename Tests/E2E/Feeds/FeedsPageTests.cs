using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedsPageTests
{
	[Test]
	public async Task CreateFeed_WhenCancelled_ReturnsToTheFeedList()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);

		await page.ButtonContaining("Cancel").First.ClickAsync();

		await Expect(page.Heading("Feeds")).ToBeVisibleAsync(Wait.Settle);
	}
}
