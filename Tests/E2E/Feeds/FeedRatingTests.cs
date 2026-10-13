using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedRatingTests
{
	private const string OptionalSection = "Filters, Sorting & Limits";

	[Test]
	public async Task Rating_OnFirstAvailableWithImdbFirst_PreviewsTheTitlesImdbRatesAtLeastSeven()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Rated"));
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable filters").ClickAsync();
		await FeedEditor.AddNumberConditionAsync(page, "Rating", "greater or equal", "7");
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(6), Wait.Preview);

		await FeedEditor.RatingStar(page).ClickAsync();
		await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "First available" }).ClickAsync();
		await FeedEditor.PutFirstAsync(page, "IMDb", steps: 1);
		await page.Keyboard.PressAsync("Escape");
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(5), Wait.Preview);
	}

	[Test]
	public async Task Votes_OnFirstAvailableWithRottenTomatoesFirst_CullsEveryTitleForHavingNoVotes()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Voted"));
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable filters").ClickAsync();
		await FeedEditor.AddNumberConditionAsync(page, "Votes", "greater or equal", "500");
		await FeedEditor.Toggle(page, "Enable custom sorting").ClickAsync();
		await page.Field("Sort by").Combobox.ClickAsync();
		await page.Option("Rating").ClickAsync();
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(9), Wait.Preview);

		await FeedEditor.RatingStar(page).ClickAsync();
		await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "First available" }).ClickAsync();
		await FeedEditor.PutFirstAsync(page, "Rotten Tomatoes® critics", steps: 2);
		await page.Keyboard.PressAsync("Escape");
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(0), Wait.Preview);
		await FeedEditor.Rows(page).First.Locator(".search-result-status-indicator").HoverAsync();
		await Expect(page.GetByText("no votes behind its rating").First).ToBeVisibleAsync();
	}

	[Test]
	public async Task Votes_WhenTheRulesInfoIsClicked_ListsTheProvidersAndTheRottenTomatoesLine()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Votes"));
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable filters").ClickAsync();
		await FeedEditor.AddNumberConditionAsync(page, "Votes", "greater or equal", "500");

		await FeedEditor.VotesInfo(page).ClickAsync();

		var popover = page.Locator(".votes-pop");
		await Expect(popover.Locator(".votes-pop-row"))
			.ToHaveTextAsync([
				new Regex("TMDB"),
				new Regex("IMDb"),
				new Regex("Rotten Tomatoes®"),
				new Regex("Metacritic"),
				new Regex("Letterboxd"),
				new Regex("Trakt"),
				new Regex("MyAnimeList"),
				new Regex("AniList"),
			]);
		await Expect(popover).ToContainTextAsync("A title only Rotten Tomatoes® or Metacritic rates has no votes and fails this rule.");
	}
}
