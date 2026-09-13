using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedsPageTests
{
	[Test]
	public async Task Feeds_WhenAuthenticated_ShowsTheFeedsHeading()
	{
		var page = await E2EApp.AdminAt(Routes.Feeds);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Expect(page.Heading("Feeds")).ToBeVisibleAsync();
	}

	[Test]
	public async Task CreateFeed_WhenOpened_ShowsTheFeedEditor()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Expect(page.GetByText("Create feed")).ToBeVisibleAsync();
	}

	[Test]
	public async Task CreateFeed_WhenCancelled_ReturnsToTheFeedList()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);

		await page.ButtonContaining("Cancel").First.ClickAsync();

		await Expect(page.Heading("Feeds")).ToBeVisibleAsync(Wait.Settle);
	}
}
