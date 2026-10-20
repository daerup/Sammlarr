using System.Net;
using System.Text;
using API.Anime;
using Contracts.Datasets;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.Anime;

public class AnimeListsTests
{
	private const string Recorded = """
		[
		{"type":"TV","anidb_id":1,"anilist_id":290,"imdb_id":["tt0286390"],"mal_id":290,"themoviedb_id":{"tv":26209},"tvdb_id":72025,"season":{"tvdb":1,"tmdb":1}},
		{"type":"TV","anidb_id":2,"anilist_id":291,"mal_id":291,"tvdb_id":72025,"season":{"tvdb":2,"tmdb":2}},
		{"type":"OVA","anidb_id":3,"anilist_id":292,"tvdb_id":72025,"season":{"tvdb":1},"episode_offset":{"tvdb":12}},
		{"type":"MOVIE","anidb_id":4,"anilist_id":199,"mal_id":199,"imdb_id":["tt0245429"],"themoviedb_id":{"movie":[129, 130]}},
		{"type":"TV","anidb_id":5,"mal_id":5114,"tvdb_id":85249,"themoviedb_id":{"tv":31911}},
		{"type":"SPECIAL","anidb_id":6}
		]
		""";

	[Test]
	public async Task Parse_ForTheShowItself_KeepsEveryIdItMapsTo()
	{
		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(Recorded)).ToListAsync();

		await Assert
			.That(entries[0])
			.IsEqualTo(new AnimeIdMapping(MediaType.Show, Some.Of(290), Some.Of(290), Some.Of(72025), Some.Of(26209), Some.Of("tt0286390"), new None()));
	}

	[Test]
	public async Task Parse_ForALaterSeasonOrAPartOfTheFirst_KeepsOnlyItsOwnIdsAndItsSeries()
	{
		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(Recorded)).ToListAsync();

		await Assert
			.That(entries[1])
			.IsEqualTo(
				new AnimeIdMapping(MediaType.Show, Some.Of(291), Some.Of(291), new None(), new None(), new None(), Some.Of(72025)) { TvdbSeason = Some.Of(2) }
			);
		await Assert
			.That(entries[2])
			.IsEqualTo(
				new AnimeIdMapping(MediaType.Show, Some.Of(292), new None(), new None(), new None(), new None(), Some.Of(72025))
				{
					TvdbSeason = Some.Of(1),
					EpisodeOffset = Some.Of(12),
				}
			);
	}

	[Test]
	public async Task Parse_ForASpecialFiledAsSeasonZero_PointsAtItsShow()
	{
		const string loveHinaAgain = """
			[{"type":"OVA","anidb_id":49,"anilist_id":190,"imdb_id":["tt0279570"],"mal_id":190,"themoviedb_id":{"tv":30992},"tvdb_id":70861,"season":{"tvdb":0,"tmdb":0},"episode_offset":{"tvdb":2,"tmdb":2}}]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(loveHinaAgain)).ToListAsync();

		await Assert
			.That(entries.Single())
			.IsEqualTo(
				new AnimeIdMapping(MediaType.Show, Some.Of(190), Some.Of(190), new None(), new None(), new None(), Some.Of(70861))
				{
					TvdbSeason = Some.Of(0),
					EpisodeOffset = Some.Of(2),
				}
			);
	}

	[Test]
	public async Task Parse_ForAnEntryWhoseIdsAnEarlierEntryClaimed_PointsAtThatShow()
	{
		const string twoEntriesOneShow = """
			[
			{"type":"TV","anilist_id":888,"mal_id":888,"tvdb_id":72719,"season":{"tvdb":1}},
			{"type":"TV","anilist_id":887,"mal_id":887,"tvdb_id":72719,"season":{"tvdb":1}}
			]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(twoEntriesOneShow)).ToListAsync();

		await Assert
			.That(entries[1])
			.IsEqualTo(
				new AnimeIdMapping(MediaType.Show, Some.Of(887), Some.Of(887), new None(), new None(), new None(), Some.Of(72719)) { TvdbSeason = Some.Of(1) }
			);
	}

	[Test]
	public async Task Parse_ForATvSpecialTypedMovie_PointsAtItsShowAsAShow()
	{
		const string showThenItsSpecial = """
			[
			{"type":"TV","anilist_id":331,"mal_id":331,"imdb_id":["tt0279077"],"tvdb_id":81095,"themoviedb_id":{"tv":36247},"season":{"tvdb":1}},
			{"type":"MOVIE","anilist_id":796,"mal_id":796,"imdb_id":["tt0279077"],"tvdb_id":81095,"themoviedb_id":{"tv":36247},"season":{"tvdb":0}}
			]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(showThenItsSpecial)).ToListAsync();

		await Assert
			.That(entries[1])
			.IsEqualTo(
				new AnimeIdMapping(MediaType.Show, Some.Of(796), Some.Of(796), new None(), new None(), new None(), Some.Of(81095)) { TvdbSeason = Some.Of(0) }
			);
	}

	[Test]
	public async Task Parse_ForAMovieWithOnlyItsShowsTmdbAndTvdbIds_PointsAtItsShow()
	{
		const string movieOfAShow = """
			[{"type":"MOVIE","anilist_id":5000,"tvdb_id":81095,"themoviedb_id":{"tv":36247},"season":{"tvdb":1}}]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(movieOfAShow)).ToListAsync();

		await Assert
			.That(entries.Single())
			.IsEqualTo(new AnimeIdMapping(MediaType.Show, Some.Of(5000), new None(), new None(), new None(), new None(), Some.Of(81095)) { TvdbSeason = Some.Of(1) });
	}

	[Test]
	public async Task Parse_ForAMovieFiledAsASpecialWithItsOwnImdbId_StaysAMovie()
	{
		const string movieAtSeasonZero = """
			[{"type":"MOVIE","anilist_id":5001,"imdb_id":["tt1234567"],"tvdb_id":81095,"season":{"tvdb":0}}]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(movieAtSeasonZero)).ToListAsync();

		await Assert
			.That(entries.Single())
			.IsEqualTo(new AnimeIdMapping(MediaType.Movie, Some.Of(5001), new None(), new None(), new None(), Some.Of("tt1234567"), new None()));
	}

	[Test]
	public async Task Parse_ForAMovieWhoseTmdbMovieIdAnEarlierMovieClaimed_PointsAtNoSeries()
	{
		const string twoMoviesOneTmdbId = """
			[
			{"type":"MOVIE","anilist_id":5002,"themoviedb_id":{"movie":4242}},
			{"type":"MOVIE","anilist_id":5003,"tvdb_id":81095,"themoviedb_id":{"movie":4242},"season":{"tvdb":1}}
			]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(twoMoviesOneTmdbId)).ToListAsync();

		await Assert
			.That(entries[1])
			.IsEqualTo(new AnimeIdMapping(MediaType.Movie, Some.Of(5003), new None(), new None(), new None(), new None(), new None()) { TvdbSeason = Some.Of(1) });
	}

	[Test]
	public async Task Parse_ForALaterSeason_KeepsItsTvdbSeasonAndEpisodeOffset()
	{
		const string thirdCour = """
			[{"type":"TV","anilist_id":5006,"mal_id":5006,"tvdb_id":81095,"season":{"tvdb":3},"episode_offset":{"tvdb":24}}]
			""";

		var entry = (await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(thirdCour)).ToListAsync()).Single();

		await Assert.That((entry.TvdbSeason, entry.EpisodeOffset)).IsEqualTo((Some.Of(3), Some.Of(24)));
	}

	[Test]
	public async Task Parse_ForAnEntryWithNothingInTheFile_KeepsItsAnimeIdsOnly()
	{
		const string bare = """
			[{"type":"TV","anilist_id":185660,"mal_id":59130}]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(bare)).ToListAsync();

		await Assert
			.That(entries.Single())
			.IsEqualTo(new AnimeIdMapping(MediaType.Show, Some.Of(185660), Some.Of(59130), new None(), new None(), new None(), new None()));
	}

	[Test]
	public async Task Parse_ForAMovie_TakesItsFirstTmdbMovie()
	{
		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(Recorded)).ToListAsync();

		await Assert
			.That(entries[3])
			.IsEqualTo(new AnimeIdMapping(MediaType.Movie, Some.Of(199), Some.Of(199), new None(), Some.Of(129), Some.Of("tt0245429"), new None()));
	}

	[Test]
	public async Task Parse_ForAMovieWithOnlyItsShowsTmdbId_TakesNoTmdbId()
	{
		const string roarOfAwakening = """
			[{"type":"MOVIE","anilist_id":100465,"themoviedb_id":{"tv":1429},"season":{"tvdb":0,"tmdb":0}}]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(roarOfAwakening)).ToListAsync();

		await Assert
			.That(entries.Single())
			.IsEqualTo(new AnimeIdMapping(MediaType.Movie, Some.Of(100465), new None(), new None(), new None(), new None(), new None()) { TvdbSeason = Some.Of(0) });
	}

	[Test]
	public async Task Parse_ForAMovieFiledAsASpecialWhoseTmdbMovieIdAnEarlierMovieClaimed_PointsAtItsShow()
	{
		const string movieThenItsTwin = """
			[
			{"type":"MOVIE","anilist_id":10702,"themoviedb_id":{"movie":12345}},
			{"type":"OVA","anilist_id":10703,"tvdb_id":81095,"themoviedb_id":{"movie":12345},"season":{"tvdb":0}}
			]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(movieThenItsTwin)).ToListAsync();

		await Assert
			.That(entries[1])
			.IsEqualTo(
				new AnimeIdMapping(MediaType.Show, Some.Of(10703), new None(), new None(), new None(), new None(), Some.Of(81095)) { TvdbSeason = Some.Of(0) }
			);
	}

	[Test]
	public async Task Parse_ForAMovieWhoseImdbIdAnEarlierShowClaimed_PointsAtThatShow()
	{
		const string showThenItsMovie = """
			[
			{"type":"TV","anilist_id":5004,"imdb_id":["tt7654321"],"tvdb_id":81095,"season":{"tvdb":1}},
			{"type":"MOVIE","anilist_id":5005,"imdb_id":["tt7654321"],"tvdb_id":81095,"season":{"tvdb":1}}
			]
			""";

		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(showThenItsMovie)).ToListAsync();

		await Assert
			.That(entries[1])
			.IsEqualTo(new AnimeIdMapping(MediaType.Show, Some.Of(5005), new None(), new None(), new None(), new None(), Some.Of(81095)) { TvdbSeason = Some.Of(1) });
	}

	[Test]
	public async Task Parse_ForAnEntryWithoutAnAniListOrMalId_LeavesItOut()
	{
		var entries = await AnimeIdsFile.Parse(Encoding.UTF8.GetBytes(Recorded)).ToListAsync();

		await Assert.That(entries).Count().IsEqualTo(5);
		await Assert.That(entries[4].Mal).IsSome().And.IsEqualTo(5114);
	}

	[Test]
	public async Task Parse_ForACutShortFile_FailsWhileReadingIt()
	{
		var cut = Encoding.UTF8.GetBytes(Recorded)[..120];

		await Assert.That(async () => await AnimeIdsFile.Parse(cut).ToListAsync()).Throws<System.Text.Json.JsonException>();
	}

	[Test]
	public async Task DownloadAsync_WithTheLastETag_AsksOnlyForAChangedFile()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotModified);

		var result = await new AnimeIdsFile(handler.CreateClient("https://raw.githubusercontent.com/Fribb/anime-lists/master/")).DownloadAsync(Some.Of("\"last\""));

		await Assert.That(result).IsSuccess().And.Multiple(async file => await Assert.That(file).IsNone());
		handler.Verify(request => request.PathMatches("/anime-list-full.json$").Header("If-None-Match", "\"last\""), Times.Once);
	}

	[Test]
	public async Task DownloadAsync_WhenTheHostCannotBeReached_FailsWithWhy()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new HttpRequestException("No such host is known."));

		var result = await new AnimeIdsFile(handler.CreateClient("https://raw.githubusercontent.com/Fribb/anime-lists/master/")).DownloadAsync(new None());

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Reason).Contains("No such host is known."));
	}

	[Test]
	public async Task LookUpAsync_BeforeTheFirstLoad_IsUnavailableWithoutAsking()
	{
		var testee = Converter(loaded: false, out _);

		await Assert
			.That(await testee.LookUpAsync(testee.RequestById(AniListId.From("290", MediaType.Show))))
			.IsOfUnionType<Answer<IdLookupResult>, Unavailable>()
			.And.IsEqualTo(new Unavailable(RemoteService.AnimeLists, new None(), false));
	}

	[Test]
	public async Task LookUpAsync_ForAMappedMalId_FindsTheShowsTvdbTmdbAndImdbIds()
	{
		var testee = Converter(loaded: true, out var map);
		var asked = MalId.From("290", MediaType.Show);
		map.FindAsync(Any(), Any())
			.Returns(Some.Of(new AnimeIdMapping(MediaType.Show, Some.Of(290), Some.Of(290), Some.Of(72025), Some.Of(26209), Some.Of("tt0286390"), new None())));

		var result = await testee.LookUpAsync(testee.RequestById(asked));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
				await Assert
					.That(found.Ids.Select(id => $"{id}"))
					.IsEquivalentTo(["AniList:Show:290", "Mal:Show:290", "Tvdb:Show:72025", "Tmdb:Show:26209", "Imdb:Show:tt0286390"])
			);
	}

	[Test]
	public async Task LookUpAsync_ForAnUnmappedId_FindsNothing()
	{
		var testee = Converter(loaded: true, out var map);
		map.FindAsync(Any(), Any()).Returns(new None());

		var result = await testee.LookUpAsync(testee.RequestById(AniListId.From("999999", MediaType.Show)));

		await Assert.That(result).IsFound().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}

	[Test]
	public async Task Lookups_TakeAnimeIdsToEveryIdAndAShowsIdsToItsAnimeIdsWithoutHintsAndWithoutACall()
	{
		var testee = Converter(loaded: true, out _);

		await Assert
			.That(testee.Lookups)
			.IsEquivalentTo([
				(Domain.Media.Conversions.IdLookup)
					new Domain.Media.Conversions.ById(
						[MediaProvider.AniList, MediaProvider.Mal],
						[MediaProvider.AniList, MediaProvider.Mal, MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb],
						0,
						GivesHints: false
					),
				new Domain.Media.Conversions.ById(
					[MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb],
					[MediaProvider.AniList, MediaProvider.Mal],
					0,
					GivesHints: false
				),
			]);
	}

	[Test]
	public async Task LookUpAsync_ByAShowsTvdbId_AnswersOnlyItsAniListAndMalIds()
	{
		var testee = Converter(loaded: true, out var map);
		map.FindAsync(Any(), Any())
			.Returns(Some.Of(new AnimeIdMapping(MediaType.Show, Some.Of(290), Some.Of(290), Some.Of(72025), Some.Of(26209), Some.Of("tt0286390"), new None())));
		var byShowId = new Domain.Media.Conversions.ById(
			[MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb],
			[MediaProvider.AniList, MediaProvider.Mal],
			0,
			GivesHints: false
		);

		var result = await testee.LookUpAsync(new ByIdRequest(byShowId, TvdbId.From("72025", MediaType.Show)));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found => await Assert.That(found.Ids.Select(id => $"{id}")).IsEquivalentTo(["AniList:Show:290", "Mal:Show:290"]));
	}

	private static AnimeListsIdConverter Converter(bool loaded, out Mock<IAnimeIds> map)
	{
		map = IAnimeIds.Mock();
		var loads = IDatasetLoads.Mock();
		loads
			.FindAsync(DatasetNames.AnimeIds, Any())
			.Returns(Optional.When(loaded, () => new DatasetLoad(DatasetNames.AnimeIds, DateTimeOffset.UnixEpoch, new None(), 5)));
		return new AnimeListsIdConverter(new AnimeIdsInRun(map.Object, loads.Object));
	}
}
