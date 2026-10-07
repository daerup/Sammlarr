using System.Net;
using System.Text;
using API.Anime;
using Contracts.Datasets;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Mocks.Http;

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
		var entries = await AnimeListsFile.Parse(Encoding.UTF8.GetBytes(Recorded)).ToListAsync();

		await Assert
			.That(entries[0])
			.IsEqualTo(new AnimeIdMapping(MediaType.Show, Some.Of(290), Some.Of(290), Some.Of(72025), Some.Of(26209), Some.Of("tt0286390")));
	}

	[Test]
	public async Task Parse_ForALaterSeasonOrAPartOfTheFirst_KeepsOnlyItsOwnIds()
	{
		var entries = await AnimeListsFile.Parse(Encoding.UTF8.GetBytes(Recorded)).ToListAsync();

		await Assert.That(entries[1]).IsEqualTo(new AnimeIdMapping(MediaType.Show, Some.Of(291), Some.Of(291), new None(), new None(), new None()));
		await Assert.That(entries[2]).IsEqualTo(new AnimeIdMapping(MediaType.Show, Some.Of(292), new None(), new None(), new None(), new None()));
	}

	[Test]
	public async Task Parse_ForAnEntryOffsetOnBothSites_KeepsOnlyItsOwnIds()
	{
		const string loveHinaAgain = """
			[{"type":"OVA","anidb_id":49,"anilist_id":190,"imdb_id":["tt0279570"],"mal_id":190,"themoviedb_id":{"tv":30992},"tvdb_id":70861,"season":{"tvdb":0,"tmdb":0},"episode_offset":{"tvdb":2,"tmdb":2}}]
			""";

		var entries = await AnimeListsFile.Parse(Encoding.UTF8.GetBytes(loveHinaAgain)).ToListAsync();

		await Assert.That(entries.Single()).IsEqualTo(new AnimeIdMapping(MediaType.Show, Some.Of(190), Some.Of(190), new None(), new None(), new None()));
	}

	[Test]
	public async Task Parse_ForAMovie_TakesItsFirstTmdbMovie()
	{
		var entries = await AnimeListsFile.Parse(Encoding.UTF8.GetBytes(Recorded)).ToListAsync();

		await Assert.That(entries[3]).IsEqualTo(new AnimeIdMapping(MediaType.Movie, Some.Of(199), Some.Of(199), new None(), Some.Of(129), Some.Of("tt0245429")));
	}

	[Test]
	public async Task Parse_ForAnEntryWithoutAnAniListOrMalId_LeavesItOut()
	{
		var entries = await AnimeListsFile.Parse(Encoding.UTF8.GetBytes(Recorded)).ToListAsync();

		await Assert.That(entries).Count().IsEqualTo(5);
		await Assert.That(entries[4].Mal).IsSome().And.IsEqualTo(5114);
	}

	[Test]
	public async Task Parse_ForACutShortFile_FailsWhileReadingIt()
	{
		var cut = Encoding.UTF8.GetBytes(Recorded)[..120];

		await Assert.That(async () => await AnimeListsFile.Parse(cut).ToListAsync()).Throws<System.Text.Json.JsonException>();
	}

	[Test]
	public async Task DownloadAsync_WithTheLastETag_AsksOnlyForAChangedFile()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotModified);

		var result = await new AnimeListsFile(handler.CreateClient("https://raw.githubusercontent.com/Fribb/anime-lists/master/")).DownloadAsync(
			Some.Of("\"last\"")
		);

		await Assert.That(result).IsSuccess().And.Multiple(async file => await Assert.That(file).IsNone());
		handler.Verify(request => request.PathMatches("/anime-list-full.json$").Header("If-None-Match", "\"last\""), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_BeforeTheFirstLoad_IsAnError()
	{
		var testee = Converter(loaded: false, out _);

		await Assert.That(await testee.LookUpAsync(testee.RequestById(AniListId.From("290", MediaType.Show)))).IsError();
	}

	[Test]
	public async Task LookUpAsync_ForAMappedMalId_FindsTheShowsTvdbTmdbAndImdbIds()
	{
		var testee = Converter(loaded: true, out var map);
		var asked = MalId.From("290", MediaType.Show);
		map.FindAsync(Any(), Any())
			.Returns(Some.Of(new AnimeIdMapping(MediaType.Show, Some.Of(290), Some.Of(290), Some.Of(72025), Some.Of(26209), Some.Of("tt0286390"))));

		var result = await testee.LookUpAsync(testee.RequestById(asked));

		await Assert
			.That(result)
			.IsSuccess()
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

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}

	[Test]
	public async Task Lookups_TakeAniListAndMalIdsToTheirCrossIdsWithoutHints()
	{
		var testee = Converter(loaded: true, out _);

		await Assert
			.That(testee.Lookups)
			.IsEquivalentTo([
				(Domain.Media.Conversions.IdLookup)
					new Domain.Media.Conversions.ById(
						[MediaProvider.AniList, MediaProvider.Mal],
						[MediaProvider.AniList, MediaProvider.Mal, MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb],
						1,
						GivesHints: false
					),
			]);
	}

	private static AnimeListsIdConverter Converter(bool loaded, out Mock<IAnimeIds> map)
	{
		map = IAnimeIds.Mock();
		var loads = IDatasetLoads.Mock();
		loads
			.FindAsync(DatasetNames.AnimeLists, Any())
			.Returns(Optional.When(loaded, () => new DatasetLoad(DatasetNames.AnimeLists, DateTimeOffset.UnixEpoch, new None(), 5)));
		return new AnimeListsIdConverter(map.Object, loads.Object);
	}
}
