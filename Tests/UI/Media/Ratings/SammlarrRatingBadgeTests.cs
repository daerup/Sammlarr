using BlazorBlueprint.Components;
using Bunit;
using Contracts.Media;
using Contracts.Ratings;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
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
	public async Task Badge_WhenTheRatingReadsAsNineAndAHalf_Shines()
	{
		var cut = Render<SammlarrRatingBadge>(parameters =>
			parameters
				.Add(badge => badge.Ratings, MediaRatings.From([new ImdbRating(9.46f, Some.Of(10))]))
				.Add(badge => badge.Strategy, Some.Of(new RatingStrategy(new OrderedRating([RatingSource.Imdb]))))
		);

		await Assert.That(cut.Find(".rating-badge").TextContent.Trim()).IsEqualTo("9.5");
		await Assert.That(cut.Find(".rating-star").ClassList).Contains("shine");
	}

	[Test]
	public async Task Breakdown_ForAChosenSourceWithoutAProvider_LinksTheWordProvider()
	{
		var cut = Render<SammlarrRatingBreakdown>(parameters =>
			parameters
				.Add(breakdown => breakdown.Ratings, ProjectHailMary)
				.Add(breakdown => breakdown.Strategy, new RatingStrategy(new AveragedRating([RatingSource.Imdb])))
				.Add(breakdown => breakdown.Available, [RatingSource.Tmdb])
				.Add(breakdown => breakdown.Where, "your default")
		);

		var reason = cut.FindAll(".rating-row-name small").First();
		await Assert.That(reason.TextContent.Trim()).IsEqualTo("needs a provider");
		await Assert.That(reason.QuerySelector(".rating-suppliers-link")).IsNotNull();
	}

	[Test]
	public async Task Badge_WhenNoSourceRatesTheTitle_ShowsTheDashedStarWithADash()
	{
		var cut = Render<SammlarrRatingBadge>(parameters => parameters.Add(badge => badge.Ratings, MediaRatings.None));

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
	public async Task Breakdown_WhenTheTitlesRottenTomatoesIdIsFound_LinksRottenTomatoesThere()
	{
		_media
			.FindIdAsync(Any<MediaId>(), MediaProvider.RottenTomatoes, Any())
			.Returns(Some.Of<IExternalMediaId>(RottenTomatoesId.From("tv/last_seen", MediaType.Show)));

		var cut = RenderBreakdown(LastSeen, new RatingStrategy(AveragedRating.Everything));

		await Assert
			.That(cut.FindAll(".rating-row-link").Select(link => link.GetAttribute("href") ?? string.Empty))
			.Contains("https://www.rottentomatoes.com/tv/last_seen");
		_media.FindIdAsync(Any<MediaId>(), MediaProvider.RottenTomatoes, Any()).WasCalled(Times.Once);
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
	public async Task Breakdown_ForEachRow_UsesItsOwnersBrandColour()
	{
		var cut = RenderBreakdown(LastSeen, new RatingStrategy(AveragedRating.Everything));

		await Assert
			.That(cut.FindAll(".rating-row").Select(row => row.GetAttribute("style") ?? string.Empty))
			.IsEquivalentTo(["--brand: #01B4E4", "--brand: #F5DE50", "--brand: #FA320A", "--brand: #FA320A"], CollectionOrdering.Matching);
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

	[Test]
	public async Task Breakdown_ForASeries_SaysAniListAndMalRateItsFirstSeason()
	{
		var cut = RenderAnime(TvdbId.From("267440", MediaType.Show), AniListId.From("16498", MediaType.Show));

		await Assert.That(Subtitles(cut)).IsEquivalentTo(["season 1 · 2,826,539 votes", "season 1 · 610,640 votes"]);
	}

	[Test]
	public async Task Breakdown_ForAMovie_GivesAniListAndMalNoSeason()
	{
		var cut = RenderAnime(TmdbId.From("129", MediaType.Movie), AniListId.From("199", MediaType.Movie));

		await Assert.That(Subtitles(cut)).IsEquivalentTo(["2,826,539 votes", "610,640 votes"]);
	}

	[Test]
	public async Task Breakdown_ForALaterSeasonStoredAsItsOwnTitle_GivesAniListAndMalNoSeason()
	{
		var cut = RenderAnime(AniListId.From("104578", MediaType.Show), MalId.From("38524", MediaType.Show));

		await Assert.That(Subtitles(cut)).IsEquivalentTo(["2,826,539 votes", "610,640 votes"]);
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
