using BlazorBlueprint.Components;
using Bunit;
using Contracts.Media;
using Contracts.Ratings;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using Observability.Usage;
using UI.Shared.Media.Ratings;
using UI.Shared.Services;

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

	private static readonly MediaRatings ProjectHailMary = MediaRatings.From([
		new TmdbRating(8.6f, Some.Of(7705)),
		new ImdbRating(8.2f, Some.Of(542195)),
		new RottenTomatoesCriticsRating(95),
		new RottenTomatoesAudienceRating(95),
	]);

	private static readonly MediaRatings LastSeen = MediaRatings.From([
		new TmdbRating(9.0f, Some.Of(8)),
		new ImdbRating(7.6f, Some.Of(303)),
		new RottenTomatoesCriticsRating(87),
	]);

	private readonly Mock<IRatingService> _ratings = IRatingService.Mock();

	private readonly Mock<IMediaService> _media = IMediaService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(_ratings.Object);
		Services.AddScoped<RatingContext>();
		Services.AddSingleton(_media.Object);
		_media.FindIdAsync(Any<MediaId>(), Any<MediaProvider>(), Any()).Returns(new None());
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
	public async Task Badge_WhenTheRatingReadsAsNineAndAHalf_DrawsTheStarsRays()
	{
		var cut = Render<SammlarrRatingBadge>(parameters =>
			parameters
				.Add(badge => badge.Ratings, MediaRatings.From([new ImdbRating(9.46f, Some.Of(10))]))
				.Add(badge => badge.Strategy, Some.Of(new RatingStrategy(new OrderedRating([RatingSource.Imdb]))))
		);

		await Assert.That(cut.Find(".rating-badge").TextContent.Trim()).IsEqualTo("9.5");
		await Assert.That(cut.FindAll(".rating-star .rating-star-rays")).HasSingleItem();
	}

	[Test]
	public async Task Breakdown_ForAChosenSourceWithoutAProvider_ListsNoRowForIt()
	{
		var cut = Render<SammlarrRatingBreakdown>(parameters =>
			parameters
				.Add(breakdown => breakdown.Ratings, ProjectHailMary)
				.Add(breakdown => breakdown.Strategy, new RatingStrategy(new AveragedRating([RatingSource.Imdb])))
				.Add(breakdown => breakdown.Available, [RatingSource.Tmdb])
				.Add(breakdown => breakdown.Where, "your default")
		);

		await Assert.That(string.Concat(cut.FindAll(".rating-row-name").Select(name => name.TextContent))).DoesNotContain("IMDb");
		await Assert.That(cut.FindAll(".rating-suppliers-link")).IsEmpty();
	}

	[Test]
	public async Task Badge_WhenNoSourceRatesTheTitle_ShowsADashAndSaysItIsUnrated()
	{
		var cut = Render<SammlarrRatingBadge>(parameters => parameters.Add(badge => badge.Ratings, MediaRatings.None));

		await Assert.That(cut.Find(".rating-badge").TextContent.Trim()).IsEqualTo("–");
		await Assert.That(cut.Find(".rating-badge").GetAttribute("aria-label")).IsEqualTo(RatingTexts.Unrated);
	}

	[Test]
	public async Task Breakdown_UnderAverage_ShowsTheVotesBehindTheRatingInItsHeading()
	{
		var cut = RenderBreakdown(ProjectHailMary, new RatingStrategy(AveragedRating.Everything));

		await Assert.That(cut.Find(".rating-breakdown-how").TextContent).Contains("Average · your default").And.Contains("549,900 votes");
	}

	[Test]
	public async Task Breakdown_UnderFirstAvailable_SaysOnceWhyTheRowsAfterThePickDoNotCount()
	{
		var cut = RenderBreakdown(ProjectHailMary, new RatingStrategy(new OrderedRating([RatingSource.Imdb])));

		await Assert.That(cut.FindAll(".rating-row-name small").Count(line => line.TextContent == "not needed, IMDb came first")).IsEqualTo(1);
	}

	[Test]
	public async Task Breakdown_UnderFirstAvailable_SaysTheEmptyRowAboveThePickWasSkipped()
	{
		var cut = RenderBreakdown(LastSeen, new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesAudience, RatingSource.Imdb])));

		await Assert.That(cut.FindAll(".rating-row-name small").Select(line => line.TextContent.Trim())).Contains("no rating, skipped");
	}

	[Test]
	public async Task Breakdown_WhenNoSourceRatesTheTitle_ShowsOnlyTheHeaderAndTheUnratedFooter()
	{
		var cut = RenderBreakdown(MediaRatings.None, new RatingStrategy(AveragedRating.Everything));

		await Assert.That(cut.FindAll(".rating-row")).IsEmpty();
		await Assert.That(cut.Markup).Contains(RatingTexts.UnratedFooter);
	}

	[Test]
	public async Task Breakdown_ForEachRatedRow_LinksItsPage()
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
		await Assert.That(cut.FindAll(".rating-row").Count).IsEqualTo(links.Count);
	}

	[Test]
	public async Task Breakdown_WhenTheTitlesRottenTomatoesIdIsFound_LinksRottenTomatoesThere()
	{
		_media
			.FindIdAsync(Any<MediaId>(), MediaProvider.RottenTomatoes, Any())
			.Returns(Some.Of<IExternalMediaId>(RottenTomatoesId.From("tv/last_seen", MediaType.Show)));

		var cut = RenderBreakdown(LastSeen, new RatingStrategy(AveragedRating.Everything));

		await Assert
			.That(cut.FindAll(".rating-row-link").Select(link => link.GetAttribute("href") ?? string.Empty))
			.Contains("https://www.rottentomatoes.com/tv/last_seen");
	}

	[Test]
	public async Task Breakdown_WhenItLooksUpAnOwnersId_AsksUnderTheShowingTitlesCause()
	{
		var cause = UsageCause.Other;
		_media.FindIdAsync(Any<MediaId>(), Any<MediaProvider>(), Any()).Returns(new None()).Callback(() => cause = UsageCauses.Current.Cause);

		RenderBreakdownWith(LastSeen, new RatingStrategy(AveragedRating.Everything), [ImdbId.From("tt0903747", MediaType.Show)]);

		await Assert.That(cause).IsEqualTo(UsageCause.ShowingTitles);
	}

	[Test]
	public void Breakdown_WhenTheTitleLacksTheIdOfARatedOwner_AsksForThatIdOnce()
	{
		RenderBreakdownWith(LastSeen, new RatingStrategy(AveragedRating.Everything), [ImdbId.From("tt0903747", MediaType.Show)]);

		_media.FindIdAsync(Any<MediaId>(), MediaProvider.Tmdb, Any()).WasCalled(Times.Once);
		_media.FindIdAsync(Any<MediaId>(), MediaProvider.RottenTomatoes, Any()).WasCalled(Times.Once);
		_media.FindIdAsync(Any<MediaId>(), MediaProvider.Imdb, Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Breakdown_WhenTheTitleAlreadyHasARottenTomatoesId_LinksThereWithoutAsking()
	{
		var cut = RenderBreakdown(LastSeen, new RatingStrategy(AveragedRating.Everything), RottenTomatoesId.From("tv/last_seen", MediaType.Show));

		await Assert
			.That(cut.FindAll(".rating-row-link").Select(link => link.GetAttribute("href") ?? string.Empty))
			.Contains("https://www.rottentomatoes.com/tv/last_seen");
		_media.FindIdAsync(Any<MediaId>(), Any<MediaProvider>(), Any()).WasCalled(Times.Never);
	}

	public static IEnumerable<Func<(IExternalMediaId[] Ids, string[] Subtitles)>> AnimeTitles()
	{
		return
		[
			() => ([TvdbId.From("267440", MediaType.Show), AniListId.From("16498", MediaType.Show)], ["season 1 · 2,826,539 votes", "season 1 · 610,640 votes"]),
			() => ([TmdbId.From("129", MediaType.Movie), AniListId.From("199", MediaType.Movie)], ["2,826,539 votes", "610,640 votes"]),
			() => ([AniListId.From("104578", MediaType.Show), MalId.From("38524", MediaType.Show)], ["2,826,539 votes", "610,640 votes"]),
		];
	}

	[Test]
	[MethodDataSource(nameof(AnimeTitles))]
	public async Task Breakdown_ForAnAnimeTitle_SaysAniListAndMalRateTheFirstSeasonOnlyOfAWholeSeries(IExternalMediaId[] ids, string[] subtitles)
	{
		var cut = RenderAnime(ids);

		await Assert.That(Subtitles(cut)).IsEquivalentTo(subtitles);
	}

	private static IEnumerable<string> Subtitles(IRenderedComponent<SammlarrRatingBreakdown> cut) =>
		cut.FindAll(".rating-row-name small").Select(line => line.TextContent.Trim());

	private IRenderedComponent<SammlarrRatingBreakdown> RenderAnime(params IExternalMediaId[] ids)
	{
		return Render<SammlarrRatingBreakdown>(parameters =>
			parameters
				.Add(breakdown => breakdown.Ratings, MediaRatings.From([new AniListRating(85, Some.Of(610_640)), new MyAnimeListRating(8.54f, Some.Of(2_826_539))]))
				.Add(breakdown => breakdown.Strategy, new RatingStrategy(new AveragedRating([RatingSource.MyAnimeList, RatingSource.AniList])))
				.Add(breakdown => breakdown.Available, [RatingSource.MyAnimeList, RatingSource.AniList])
				.Add(breakdown => breakdown.Where, "your default")
				.Add(breakdown => breakdown.Ids, ids)
		);
	}

	private IRenderedComponent<SammlarrRatingBreakdown> RenderBreakdown(MediaRatings ratings, RatingStrategy strategy, params IExternalMediaId[] known)
	{
		return RenderBreakdownWith(ratings, strategy, [TmdbId.From("1396", MediaType.Show), ImdbId.From("tt0903747", MediaType.Show), .. known]);
	}

	private IRenderedComponent<SammlarrRatingBreakdown> RenderBreakdownWith(MediaRatings ratings, RatingStrategy strategy, IReadOnlyList<IExternalMediaId> ids)
	{
		return Render<SammlarrRatingBreakdown>(parameters =>
			parameters
				.Add(breakdown => breakdown.Ratings, ratings)
				.Add(breakdown => breakdown.Strategy, strategy)
				.Add(breakdown => breakdown.Available, Everything)
				.Add(breakdown => breakdown.Where, "your default")
				.Add(breakdown => breakdown.Title, "Last Seen")
				.Add(breakdown => breakdown.Ids, ids)
				.Add(breakdown => breakdown.Media, Some.Of(MediaId.New()))
		);
	}
}
