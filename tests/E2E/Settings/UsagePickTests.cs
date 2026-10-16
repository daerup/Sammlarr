using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class UsagePickTests
{
	private static readonly TimeSpan Exported = TimeSpan.FromSeconds(150);

	[Test]
	public async Task Pick_ForMdblistAfterAFeedRun_NarrowsEveryCard()
	{
		var name = TestData.Name("Usage");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);
		var feed = page.FeedCard(name);
		await Expect(feed).ToBeVisibleAsync(Wait.Settle);
		await feed.Labelled("Run").ClickAsync();
		var confirm = page.GetByRole(AriaRole.Dialog).Button("Run");
		await Expect(confirm).ToBeEnabledAsync(Wait.Preview);
		await confirm.ClickAsync();
		await Expect(feed.GetByTestId("last-run-bar")).ToBeVisibleAsync(Wait.Preview);

		await UntilCountedAsync(page, "Feed runs");
		await page.Locator("[data-testid=usage-service][data-service=Mdblist]").ClickAsync();

		foreach (var card in new[] { "usage-calls", "usage-causes", "usage-feeds" })
		{
			await Expect(page.Locator($"[data-testid={card}] [data-testid=usage-pick-chip][data-service=Mdblist]")).ToBeVisibleAsync(Wait.Settle);
		}

		await Expect(Cause(page, "Feed runs")).ToBeVisibleAsync();
		await Expect(page.Locator($"[data-testid=usage-feed][data-feed='{name}']")).ToBeVisibleAsync();

		await page.Locator("[data-testid=usage-causes] [data-testid=usage-pick-chip] button[aria-label='Show every service']").ClickAsync();

		await Expect(page.GetByTestId("usage-pick-chip")).ToHaveCountAsync(0, Wait.Settle);
	}

	[Test]
	public async Task Preview_InTheFeedEditor_CountsAsFeedEditor()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, TestData.Name("Usage preview"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await UntilCountedAsync(page, "Feed editor");
		await Cause(page, "Feed editor").ClickAsync();

		await Expect(page.GetByTestId("usage-cause-feed").Filter(new LocatorFilterOptions { HasText = "New feed, not saved yet, or deleted" }))
			.ToBeVisibleAsync(Wait.Settle);
	}

	private static ILocator Cause(IPage page, string name) => page.GetByTestId("usage-cause").Filter(new LocatorFilterOptions { HasText = name });

	private static async Task UntilCountedAsync(IPage page, string cause)
	{
		var deadline = DateTimeOffset.UtcNow + Exported;
		while (true)
		{
			await SettingsPage.VisitSectionAsync(page, "usage");
			await Expect(page.Locator("[data-testid=usage-calls], [data-testid=usage-empty]").First).ToBeVisibleAsync(Wait.Settle);
			if (await page.Locator("[data-testid=usage-service][data-service=Mdblist]").CountAsync() > 0 && await Cause(page, cause).CountAsync() > 0)
			{
				return;
			}

			if (DateTimeOffset.UtcNow > deadline)
			{
				throw new TimeoutException($"Settings › Usage counted no MDBList call as {cause} within {Exported}");
			}

			await Task.Delay(5000);
		}
	}
}
