using System.Text.Json;
using API.Mdblist.Types;
using Domain.Media;

namespace API.Tests.Mdblist;

public class RootConverterTests
{
	[Test]
	public async Task Deserialize_AllStrict_ProducesMoviesShowsAndEmptyUnmatched()
	{
		const string json = """
			{
				"movies": [
					{ "id": 1, "ids": { "imdb": "tt1", "tmdb": 11, "tvdb": null, "mdblist": null, "mal": null }, "rank": 1, "title": "Movie A", "status": "released", "country": "US", "runtime": 120, "mediatype": "movie", "release_date": "2020-01-01", "release_year": 2020, "spoken_language": "en" }
				],
				"shows": [
					{ "id": 2, "ids": { "imdb": "tt2", "tmdb": 22, "tvdb": null, "mdblist": null, "mal": null }, "rank": 1, "title": "Show A", "status": "ended", "country": "US", "runtime": 45, "mediatype": "show", "release_date": "2019-05-05", "release_year": 2019, "spoken_language": "en" }
				]
			}
			""";

		var root = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(json)!;

		await Assert.That(root.movies).Count().IsEqualTo(1);
		await Assert.That(root.shows).Count().IsEqualTo(1);
		await Assert.That(root.unmatched).IsEmpty();
		await Assert.That(root.movies[0].title).IsEqualTo("Movie A");
		await Assert.That(root.shows[0].title).IsEqualTo("Show A");
	}

	[Test]
	public async Task Deserialize_MovieWithNullRuntime_RoutedToUnmatchedWithMediaTypeMovie()
	{
		const string json = """
			{
				"movies": [
					{ "id": 1, "ids": { "imdb": "tt1", "tmdb": 11, "tvdb": null, "mdblist": null, "mal": null }, "rank": 1, "title": "Broken Movie", "status": "released", "country": "US", "runtime": null, "mediatype": "movie", "release_date": "2020-01-01", "release_year": 2020, "spoken_language": "en" }
				],
				"shows": []
			}
			""";

		var root = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(json)!;

		await Assert.That(root.movies).IsEmpty();
		await Assert.That(root.unmatched).Count().IsEqualTo(1);
		await Assert.That(root.unmatched[0].MediaType).IsEqualTo(MediaType.Movie);
		await Assert.That(root.unmatched[0].title).IsEqualTo("Broken Movie");
		await Assert.That(root.unmatched[0].release_year).IsEqualTo(2020);
		await Assert.That(root.unmatched[0].ids.imdb).IsEqualTo("tt1");
		await Assert.That(root.unmatched[0].ids.tmdb).IsEqualTo(11);
	}

	[Test]
	public async Task Deserialize_ShowWithMissingTitle_RoutedToUnmatchedWithMediaTypeShow()
	{
		const string json = """
			{
				"movies": [],
				"shows": [
					{ "id": 2, "ids": { "imdb": "tt2", "tmdb": null, "tvdb": null, "mdblist": null, "mal": null }, "rank": 1, "status": "ended", "country": "US", "runtime": 45, "mediatype": "show", "release_date": "2019-05-05", "release_year": 2019, "spoken_language": "en" }
				]
			}
			""";

		var root = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(json)!;

		await Assert.That(root.shows).IsEmpty();
		await Assert.That(root.unmatched).Count().IsEqualTo(1);
		await Assert.That(root.unmatched[0].MediaType).IsEqualTo(MediaType.Show);
		await Assert.That(root.unmatched[0].title).IsEqualTo(string.Empty);
		await Assert.That(root.unmatched[0].ids.imdb).IsEqualTo("tt2");
	}

	[Test]
	public async Task Deserialize_MalformedIdsBlock_UnmatchedGetsEmptyIds()
	{
		const string json = """
			{
				"movies": [
					{ "id": 1, "ids": null, "rank": 1, "title": "No Ids", "status": "released", "country": "US", "runtime": 120, "mediatype": "movie", "release_date": "2020-01-01", "release_year": 2020, "spoken_language": "en" }
				],
				"shows": []
			}
			""";

		var root = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(json)!;

		await Assert.That(root.movies).IsEmpty();
		await Assert.That(root.unmatched).Count().IsEqualTo(1);
		await Assert.That(root.unmatched[0].title).IsEqualTo("No Ids");
		await Assert.That(root.unmatched[0].ids.imdb).IsNull();
		await Assert.That(root.unmatched[0].ids.tmdb).IsNull();
	}

	[Test]
	public async Task Deserialize_ABrokenMovieBeforeAGoodOne_StillReadsTheGoodOneAfterIt()
	{
		const string json = """
			{
				"movies": [
					{ "id": 2, "ids": { "imdb": "tt2", "tmdb": 22, "tvdb": null, "mdblist": null, "mal": null }, "rank": 1, "title": "Broken Movie", "status": "released", "country": "US", "runtime": null, "mediatype": "movie", "release_date": "2021-01-01", "release_year": 2021, "spoken_language": "en" },
					{ "id": 1, "ids": { "imdb": "tt1", "tmdb": 11, "tvdb": null, "mdblist": null, "mal": null }, "rank": 2, "title": "Good Movie", "status": "released", "country": "US", "runtime": 120, "mediatype": "movie", "release_date": "2020-01-01", "release_year": 2020, "spoken_language": "en" }
				],
				"shows": []
			}
			""";

		var root = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(json)!;

		await Assert.That(root.unmatched.Select(movie => movie.title)).IsEquivalentTo(["Broken Movie"]);
		await Assert.That(root.movies.Select(movie => movie.title)).IsEquivalentTo(["Good Movie"]);
	}
}
