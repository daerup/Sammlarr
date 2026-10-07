using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Browse;

public partial class RatingBreakdownTests
{
	[Test]
	public async Task Breakdown_ForATitleWikidataKnows_LinksEveryRatedRowToItsPage()
	{
		await using var wikidata = await E2EApp.StubAsync(Sparql("tt33764258", "m/the_odyssey_2026"));
		var page = await E2EApp.AdminAt(Routes.Browse);

		var breakdown = await OpenBreakdownAsync(page, "The Odyssey");

		await Expect(Link(breakdown, "TMDB")).ToHaveAttributeAsync("href", TmdbMoviePage());
		await Expect(Link(breakdown, "IMDb")).ToHaveAttributeAsync("href", "https://www.imdb.com/title/tt33764258");
		await Expect(Link(breakdown, "Rotten Tomatoes critics")).ToHaveAttributeAsync("href", "https://www.rottentomatoes.com/m/the_odyssey_2026");
	}

	[Test]
	public async Task Breakdown_ForATitleWikidataHasNoPageFor_LinksRottenTomatoesToItsSearch()
	{
		await using var wikidata = await E2EApp.StubAsync(Sparql("tt34564059"));
		var page = await E2EApp.AdminAt(Routes.Browse);
		var asked = await E2EApp.RequestCountAsync("%22tt34564059%22");

		var breakdown = await OpenBreakdownAsync(page, "The Runner");

		await E2EApp.WaitForRequestsBeyondAsync("%22tt34564059%22", asked);
		await Expect(Link(breakdown, "Rotten Tomatoes critics")).ToHaveAttributeAsync("href", "https://www.rottentomatoes.com/search?search=The%20Runner");
	}

	private static async Task<ILocator> OpenBreakdownAsync(IPage page, string title)
	{
		var tile = page.Locator("[data-testid='media-tile']", new PageLocatorOptions { HasText = title });
		await Expect(tile).ToBeVisibleAsync(Wait.Settle);
		await tile.ClickAsync();

		var star = page.Locator(".media-detail-sheet-content .rating-badge");
		await Expect(star).ToBeVisibleAsync(Wait.Settle);
		await star.ClickAsync();

		var breakdown = page.Locator(".rating-breakdown");
		await Expect(breakdown).ToBeVisibleAsync(Wait.Settle);
		return breakdown;
	}

	private static ILocator Link(ILocator breakdown, string source)
	{
		return breakdown.Locator(".rating-row", new LocatorLocatorOptions { HasText = source }).First.Locator(".rating-row-link");
	}

	private static StubSpec Sparql(string imdb, params string[] paths)
	{
		var bindings = string.Join(',', paths.Select(path => $$$"""{"rt":{"type":"literal","value":"{{{path}}}"}}"""));
		return new StubSpec("/sparql", 200, $$$"""{"results":{"bindings":[{{{bindings}}}]}}""")
		{
			Query = new Dictionary<string, string>
			{
				["query"] =
					$$"""SELECT ?rt ?tmdb ?tvdb ?metacritic ?letterboxd WHERE { ?item wdt:P345 "{{imdb}}" . OPTIONAL { ?item wdt:P1258 ?rt . } OPTIONAL { ?item wdt:P4947 ?tmdb . } OPTIONAL { ?item wdt:P12196 ?tvdb . } OPTIONAL { ?item wdt:P1712 ?metacritic . } OPTIONAL { ?item wdt:P6127 ?letterboxd . } }""",
			},
		};
	}

	[GeneratedRegex(@"^https://www\.themoviedb\.org/movie/\d+$")]
	private static partial Regex TmdbMoviePage();
}
