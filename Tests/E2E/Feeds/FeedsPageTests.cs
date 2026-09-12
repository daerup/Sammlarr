using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedsPageTests
{
	[Test]
	public async Task Feeds_WhenAuthenticated_ShowsTheFeedsHeading()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/feeds");

		await Assert.That(page.Url).DoesNotContain("/signin");
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Feeds" })).ToBeVisibleAsync();
	}

	[Test]
	public async Task CreateFeed_WhenOpened_ShowsTheFeedEditor()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/feeds/create");

		await Assert.That(page.Url).DoesNotContain("/signin");
		await Expect(page.GetByText("Create feed")).ToBeVisibleAsync();
	}

	[Test]
	public async Task CreateFeed_WhenCancelled_ReturnsToTheFeedList()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cancel" }).First.ClickAsync();

		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Feeds" }))
			.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
	}
}
