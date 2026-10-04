using BlazorBlueprint.Components;
using Bunit;
using Contracts.Ratings;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Media.Ratings;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Media.Ratings;

public class SammlarrRatingBadgeTests : BunitContext
{
	private static readonly ValueSet<RatingSource> Everything =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
	];

	private static readonly RatingSummary ProjectHailMary = RatingSummary.From([
		new TmdbRating(8.6f, Some.Of(7705)),
		new ImdbRating(8.2f, Some.Of(542195)),
		new RottenTomatoesCriticsRating(95),
		new RottenTomatoesAudienceRating(95),
	]);

	private static readonly RatingSummary LastSeen = RatingSummary.From([
		new TmdbRating(9.0f, Some.Of(8)),
		new ImdbRating(7.6f, Some.Of(303)),
		new RottenTomatoesCriticsRating(87),
	]);

	private readonly Mock<IRatingService> _ratings = IRatingService.Mock();

	private readonly Mock<IRottenTomatoesPages> _pages = IRottenTomatoesPages.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddSingleton(_ratings.Object);
		Services.AddScoped<RatingContext>();
		Services.AddSingleton(_pages.Object);
		_pages.FindPathAsync(Any<MediaId>(), Any()).Returns(new None());
		JSInterop.Mode = JSRuntimeMode.Loose;
		_ratings.GetDefaultAsync(Any()).Returns(new RatingStrategy(new OrderedRating([RatingSource.Imdb])));
		_ratings.GetAvailableAsync(Any()).Returns(Everything);
	}

	[Test]
	public async Task Badge_UnderYourDefault_ShowsTheRatingItResolvesTo()
	{
		var cut = Render<SammlarrRatingBadge>(parameters => parameters.Add(badge => badge.Ratings, ProjectHailMary));

		await Assert.That(cut.Find(".rating-badge").TextContent.Trim()).IsEqualTo("8.2");
		await Assert.That(cut.Find(".rating-badge").GetAttribute("aria-label")).IsEqualTo("Sammlarr rating 8.2, show how it came about");
	}

	[Test]
	public async Task Badge_ForAFeedWithItsOwnStrategy_ShowsTheFeedsRating()
	{
		var cut = Render<SammlarrRatingBadge>(parameters =>
			parameters
				.Add(badge => badge.Ratings, ProjectHailMary)
				.Add(badge => badge.Strategy, Some.Of(new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesCritics]))))
		);

		await Assert.That(cut.Find(".rating-badge").TextContent.Trim()).IsEqualTo("9.5");
	}

	[Test]
	public async Task Badge_WhenNoSourceRatesTheTitle_ShowsTheDashedStarWithADash()
	{
		var cut = Render<SammlarrRatingBadge>(parameters => parameters.Add(badge => badge.Ratings, RatingSummary.None));

		await Assert.That(cut.Find(".rating-badge").TextContent.Trim()).IsEqualTo("–");
		await Assert.That(cut.Find(".rating-badge").ClassList).Contains("is-unrated");
		await Assert.That(cut.Find(".rating-star path").GetAttribute("stroke-dasharray")).IsNotNull();
	}

	[Test]
	public async Task Breakdown_UnderAverage_ShowsTheVotesBehindTheRatingInItsHeading()
	{
		var cut = RenderBreakdown(ProjectHailMary, new RatingStrategy(AveragedRating.Everything));

		await Assert.That(cut.Find(".rating-breakdown-how").TextContent).Contains("Average · your default").And.Contains("549,900 votes");
	}

	[Test]
	public async Task Breakdown_UnderFirstAvailable_DimsTheRowsThatDoNotCountAndSaysWhyOnce()
	{
		var cut = RenderBreakdown(ProjectHailMary, new RatingStrategy(new OrderedRating([RatingSource.Imdb])));

		await Assert.That(cut.FindAll(".rating-row.dim").Count).IsEqualTo(3);
		await Assert.That(cut.FindAll(".rating-row-name small").Count(line => line.TextContent == "not needed, IMDb came first")).IsEqualTo(1);
	}

	[Test]
	public async Task Breakdown_RowsWithARatingLink_ARowWithoutOneDoesNot()
	{
		var cut = RenderBreakdown(LastSeen, new RatingStrategy(AveragedRating.Everything));

		var links = cut.FindAll(".rating-row-link").Select(link => link.GetAttribute("href") ?? string.Empty).ToList();
		await Assert
			.That(links)
			.IsEquivalentTo([
				"https://www.themoviedb.org/tv/1396",
				"https://www.imdb.com/title/tt0903747",
				"https://www.rottentomatoes.com/search?search=Last%20Seen",
			]);
		await Assert.That(cut.FindAll(".rating-row").Last().QuerySelector(".rating-row-link")).IsNull();
	}

	[Test]
	public async Task Breakdown_WhenWikidataKnowsTheTitlesPage_LinksRottenTomatoesThere()
	{
		_pages.FindPathAsync(Any<MediaId>(), Any()).Returns(Some.Of("tv/last_seen"));

		var cut = RenderBreakdown(LastSeen, new RatingStrategy(AveragedRating.Everything));

		await Assert
			.That(cut.FindAll(".rating-row-link").Select(link => link.GetAttribute("href") ?? string.Empty))
			.Contains("https://www.rottentomatoes.com/tv/last_seen");
		_pages.FindPathAsync(Any<MediaId>(), Any()).WasCalled(Times.Once);
	}

	private IRenderedComponent<SammlarrRatingBreakdown> RenderBreakdown(RatingSummary ratings, RatingStrategy strategy)
	{
		return Render<SammlarrRatingBreakdown>(parameters =>
			parameters
				.Add(breakdown => breakdown.Ratings, ratings)
				.Add(breakdown => breakdown.Strategy, strategy)
				.Add(breakdown => breakdown.Available, Everything)
				.Add(breakdown => breakdown.Where, "your default")
				.Add(breakdown => breakdown.Title, "Last Seen")
				.Add(breakdown => breakdown.Ids, [TmdbId.From("1396", MediaType.Show), ImdbId.From("tt0903747", MediaType.Show)])
				.Add(breakdown => breakdown.Media, Some.Of(MediaId.New()))
		);
	}
}
