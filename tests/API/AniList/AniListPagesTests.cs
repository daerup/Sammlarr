using API.AniList;
using API.Feeds;
using TestInfrastructure;

namespace API.Tests.AniList;

public class AniListPagesTests
{
	private static readonly DateOnly Today = new(2026, 10, 6);

	private static readonly HashSet<string> Genres = ["Action", "Comedy"];

	[Test]
	[Arguments("search/anime?search=frieren", """{"type":"ANIME","search":"frieren"}""")]
	[Arguments("search/anime?year=2024", """{"type":"ANIME","year":"2024%"}""")]
	[Arguments("search/anime?year=2024&season=FALL", """{"type":"ANIME","seasonYear":2024,"season":"FALL"}""")]
	[Arguments("search/anime?format=TV&format=ONA", """{"type":"ANIME","format":["TV","ONA"]}""")]
	[Arguments("search/anime?airing%20status=RELEASING", """{"type":"ANIME","status":"RELEASING"}""")]
	[Arguments("search/anime?source%20material=MANGA", """{"type":"ANIME","source":"MANGA"}""")]
	[Arguments("search/anime?country%20of%20origin=JP", """{"type":"ANIME","countryOfOrigin":"JP"}""")]
	[Arguments("search/anime?year%20range=2010&year%20range=2040", """{"type":"ANIME","yearGreater":20099999,"yearLesser":20410000}""")]
	[Arguments("search/anime?sort=SCORE_DESC", """{"type":"ANIME","sort":["SCORE_DESC"]}""")]
	[Arguments("search/anime?sort=TRENDING_DESC", """{"type":"ANIME","sort":["TRENDING_DESC","POPULARITY_DESC"]}""")]
	[Arguments("search/anime?sort=SEARCH_MATCH", """{"type":"ANIME","sort":["POPULARITY_DESC"]}""")]
	[Arguments("search/anime?search=frieren&sort=SEARCH_MATCH", """{"type":"ANIME","search":"frieren","sort":["SEARCH_MATCH"]}""")]
	[Arguments("search/anime?minimumTagRank=60", """{"type":"ANIME","minimumTagRank":60}""")]
	[Arguments("search/anime?excludeGenres=Comedy&excludeGenres=Isekai", """{"type":"ANIME","excludedGenres":["Comedy"],"excludedTags":["Isekai"]}""")]
	[Arguments("search/anime/trending", """{"type":"ANIME","sort":["TRENDING_DESC","POPULARITY_DESC"]}""")]
	[Arguments("search/anime/popular", """{"type":"ANIME","sort":["POPULARITY_DESC"]}""")]
	[Arguments("search/anime/top-100", """{"type":"ANIME","sort":["SCORE_DESC"]}""")]
	[Arguments("search/anime/new", """{"type":"ANIME","sort":["ID_DESC"]}""")]
	[Arguments("search/anime/favorites", """{"type":"ANIME","sort":["FAVOURITES_DESC"]}""")]
	[Arguments("search/anime/top-tv", """{"type":"ANIME","format":["TV"],"sort":["SCORE_DESC"]}""")]
	[Arguments("search/anime/top-tv-shorts", """{"type":"ANIME","format":["TV_SHORT"],"sort":["SCORE_DESC"]}""")]
	[Arguments("search/anime/top-movies", """{"type":"ANIME","format":["MOVIE"],"sort":["SCORE_DESC"]}""")]
	[Arguments("search/anime/top-ova", """{"type":"ANIME","format":["OVA"],"sort":["SCORE_DESC"]}""")]
	[Arguments("search/anime/top-ona", """{"type":"ANIME","format":["ONA"],"sort":["SCORE_DESC"]}""")]
	[Arguments("search/anime/this-season", """{"type":"ANIME","season":"FALL","seasonYear":2026}""")]
	[Arguments("search/anime/next-season", """{"type":"ANIME","season":"WINTER","seasonYear":2027}""")]
	public async Task Variables_ForEachLabelAndPreset_AreThoseAniListsPageSends(string stored, string variables)
	{
		var page = Page(stored);

		var asked = AniListPages.Variables(page, Genres, Today).Map(found => found.ToJsonString());

		await Assert.That(asked).IsSuccess().And.IsEqualTo(variables);
	}

	[Test]
	public async Task Variables_ForGenresAndTags_SplitsThemByAniListsGenres()
	{
		var asked = AniListPages.Variables(Page("search/anime?genres=Action&genres=Isekai"), Genres, Today).Map(found => found.ToJsonString());

		await Assert.That(asked).IsSuccess().And.IsEqualTo("""{"type":"ANIME","genres":["Action"],"tags":["Isekai"]}""");
	}

	[Test]
	[Arguments("search/anime?year=soon")]
	[Arguments("search/anime?year%20range=2010")]
	public async Task OfAndVariables_ForAYearItCannotRead_AreErrors(string stored)
	{
		await Assert.That(AniListPages.Of(Page(stored))).IsError();
		await Assert.That(AniListPages.Variables(Page(stored), Genres, Today)).IsError();
	}

	[Test]
	[Arguments("search/anime?streaming%20on=Netflix", "streaming on")]
	[Arguments("search/anime?episodes=1&episodes=12", "episodes")]
	[Arguments("search/anime?duration=1&duration=30", "duration")]
	[Arguments("search/manga", "search/manga")]
	[Arguments("search/anime/top-manhwa", "top-manhwa")]
	[Arguments("studio/569/MAPPA", "studio/569/MAPPA")]
	[Arguments("staff/100088/Tetsurou-Araki", "staff/100088")]
	public async Task Of_APageOrLabelItCannotFollow_IsAnErrorNamingIt(string stored, string named)
	{
		await Assert.That(AniListPages.Of(Page(stored))).IsError().And.Multiple(async error => await Assert.That(error.Reason).Contains(named));
	}

	[Test]
	[Arguments("search/anime")]
	[Arguments("search/anime/this-season")]
	[Arguments("search/anime?genres=Action&year=2024")]
	public async Task Of_ASearchOrPreset_IsFollowed(string stored)
	{
		await Assert.That(AniListPages.Of(Page(stored))).IsSuccess();
	}

	private static SiteLink Page(string stored) => SiteLink.Parse(stored, "anilist.co").GetValueOr(() => throw new InvalidOperationException(stored));
}
