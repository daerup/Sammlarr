using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedsPageTests
{
	[Test]
	public async Task CreateFeed_WhenClosedUntouched_ReturnsToTheFeedList()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);

		await FeedEditor.CloseButton(page).ClickAsync();

		await Expect(page.Heading("Feeds")).ToBeVisibleAsync(Wait.Settle);
	}
}
