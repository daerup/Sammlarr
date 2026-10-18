using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedLiveRunTests
{
	[Test]
	public async Task Run_FromTheFeedsPage_ShowsTheViewThenFoldsIntoTheCard()
	{
		var name = TestData.Name("Live");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.LoadAsync(page);
		await FeedEditor.SaveAndCloseAsync(page);
		var card = page.FeedCard(name);
		await Expect(card).ToBeVisibleAsync(Wait.Settle);

		await card.Labelled("Run").ClickAsync();
		var confirm = page.GetByRole(AriaRole.Dialog).Button("Run");
		await Expect(confirm).ToBeEnabledAsync(Wait.Preview);
		await confirm.ClickAsync();

		var live = page.GetByTestId("feed-run-live");
		await Expect(live).ToBeVisibleAsync(Wait.Settle);
		await Expect(live).ToHaveAttributeAsync("aria-label", $"Live run of {name}");
		await Expect(card.GetByTestId("running-pill")).ToBeVisibleAsync(Wait.Settle);
		await Expect(live).ToBeHiddenAsync(Wait.Preview);
		await Expect(page.GetByTestId("feed-run-idle")).ToBeVisibleAsync(Wait.Settle);
		await Expect(card.GetByTestId("last-run-bar")).ToBeVisibleAsync(Wait.Settle);
	}
}
