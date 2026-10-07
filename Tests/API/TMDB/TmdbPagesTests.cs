using API.Feeds;
using API.Tmdb;
using TestInfrastructure;

namespace API.Tests.TMDB;

public class TmdbPagesTests
{
	[Test]
	[Arguments("movie", "movie", "movie/popular?page=1")]
	[Arguments("movie/now-playing?view=calendar", "movie/now-playing", "movie/now_playing?page=1")]
	[Arguments("movie/upcoming?region=CH", "movie/upcoming?region=CH", "movie/upcoming?region=CH&page=1")]
	[Arguments("movie/top-rated", "movie/top-rated", "movie/top_rated?page=1")]
	[Arguments("movie/top-rated?language=de-DE", "movie/top-rated?language=de-DE", "movie/top_rated?language=de-DE&page=1")]
	[Arguments("tv/airing-today?timezone=Europe%2FZurich", "tv/airing-today?timezone=Europe%2FZurich", "tv/airing_today?timezone=Europe%2FZurich&page=1")]
	[Arguments("tv", "tv", "tv/popular?page=1")]
	[Arguments("tv/airing-today", "tv/airing-today", "tv/airing_today?page=1")]
	[Arguments("tv/on-the-air", "tv/on-the-air", "tv/on_the_air?page=1")]
	[Arguments("tv/top-rated", "tv/top-rated", "tv/top_rated?page=1")]
	[Arguments(
		"discover/movie?with_genres=28&sort_by=vote_average.desc",
		"discover/movie?with_genres=28&sort_by=vote_average.desc",
		"discover/movie?with_genres=28&sort_by=vote_average.desc&page=1"
	)]
	[Arguments("discover/tv?with_genres=16", "discover/tv?with_genres=16", "discover/tv?with_genres=16&page=1")]
	[Arguments("movie?with_genres=28", "movie?with_genres=28", "discover/movie?with_genres=28&page=1")]
	[Arguments("keyword/9715-superhero/movie", "keyword/9715/movie", "discover/movie?with_keywords=9715&page=1")]
	[Arguments("company/420-marvel-studios/tv", "company/420/tv", "discover/tv?with_companies=420&page=1")]
	[Arguments("network/213-netflix/tv", "network/213/tv", "discover/tv?with_networks=213&page=1")]
	[Arguments(
		"discover/movie?with_watch_providers=8&watch_region=CH",
		"discover/movie?with_watch_providers=8&watch_region=CH",
		"discover/movie?with_watch_providers=8&watch_region=CH&page=1"
	)]
	public async Task OfAndRequest_ForEachPage_StoreItAndAskTheApiForTheSameTitles(string pasted, string stored, string request)
	{
		var page = TmdbPages.Of(Page(pasted));

		await Assert.That(page.Map(found => found.Stored())).IsSuccess().And.IsEqualTo(stored);
		await Assert.That(page.Map(found => TmdbPages.Request(found, 1))).IsSuccess().And.IsEqualTo(request);
	}

	[Test]
	[Arguments("discover/movie?with_networks=213", "with_networks")]
	[Arguments("movie/now-playing?with_genres=28", "with_genres")]
	[Arguments("discover/tv?with_watch_monetization_types=flatrate", "watch_region")]
	[Arguments("discover/movie?certification=PG-13", "certification_country")]
	[Arguments("discover/movie?with_watch_providers=8", "watch_region")]
	[Arguments("movie?discover_preset=new-on-streaming", "new-on-streaming")]
	[Arguments("tv/upcoming?view=calendar", "tv/upcoming")]
	[Arguments("collection/1241-harry-potter-collection", "collection/1241-harry-potter-collection")]
	[Arguments("person/525-christopher-nolan", "person/525-christopher-nolan")]
	[Arguments("trending", "trending")]
	[Arguments("award", "award")]
	[Arguments("movie/550", "movie/550")]
	public async Task Of_APageOrKeyItCannotFollow_IsAnErrorNamingIt(string pasted, string named)
	{
		await Assert.That(TmdbPages.Of(Page(pasted))).IsError().And.Multiple(async error => await Assert.That(error.Reason).Contains(named));
	}

	private static SiteLink Page(string pasted) => SiteLink.Parse(pasted, "themoviedb.org").GetValueOr(() => throw new InvalidOperationException(pasted));
}
