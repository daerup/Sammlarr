using System.Net;
using API.Mdblist;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistIdConverterTests
{
	[Test]
	[Arguments("imdb", "tt0903747")]
	[Arguments("tmdb", "1396")]
	[Arguments("tvdb", "81189")]
	[Arguments("trakt", "1388")]
	public async Task LookUpAsync_ByEachIdItAccepts_FindsTheShowsTvdbId(string source, string value)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("show.json"));
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ShowId(source, value)));

		await Assert.That(result).IsFound().And.Multiple(async found => await Assert.That(found.Ids).Contains(TvdbId.From("81189", MediaType.Show)));
		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches($"^/{source}/show(\\?.*)?$"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ByAMalId_AsksForItUnderMal()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""[{"title":"Cowboy Bebop","year":1998,"ids":{"imdb":"tt0213338","tmdb":30991,"tvdb":76885,"trakt":1394,"mal":1,"mdblist":"bebp"},"ratings":[]}]"""
			);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(MalId.From("1", MediaType.Show)));

		await Assert.That(result).IsFound().And.Multiple(async found => await Assert.That(found.Ids).Contains(TvdbId.From("76885", MediaType.Show)));
		handler.Verify(request => request.PathMatches("^/mal/show(\\?.*)?$"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ForAShow_HintsAtItsTitleYearAndType()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("show.json"));
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0903747", MediaType.Show)));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
				await Assert
					.That(found.Hints)
					.IsSome()
					.And.IsEqualTo(
						new IdentifyHints
						{
							Title = "Breaking Bad",
							Year = 2008,
							MediaType = MediaType.Show,
						}
					)
			);
	}

	[Test]
	public async Task LookUpAsync_ForATitleMdblistDoesNotKnow_FindsNoIdAndNoHints()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("[]");
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000001", MediaType.Show)));

		await Assert.That(result).IsFound().And.Multiple(async found => await Assert.That(found).IsEqualTo(new IdLookupResult([], new None())));
	}

	[Test]
	public async Task LookUpAsync_WhenMdblistIsDown_FailsAsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.BadGateway);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0903747", MediaType.Show)));

		await Assert.That(result).IsOfUnionType<Answer<IdLookupResult>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Mdblist, new None(), true));
	}

	[Test]
	public async Task LookUpAsync_TheEnricherAndTheConverterAskingTogether_ShareOneRequest()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("show.json"));
		var titles = MdblistApis.Titles(handler);
		var testee = new MdblistIdConverter(titles);
		var breakingBad = ImdbId.From("tt0903747", MediaType.Show);

		await Task.WhenAll(testee.LookUpAsync(testee.RequestById(breakingBad)), new MdblistEnricher(titles).LookUpAsync(breakingBad, []));

		handler.Verify(request => request.PathMatches(".*"), Times.Once);
	}

	private static IExternalMediaId ShowId(string source, string value)
	{
		return source switch
		{
			"imdb" => ImdbId.From(value, MediaType.Show),
			"tmdb" => TmdbId.From(value, MediaType.Show),
			"tvdb" => TvdbId.From(value, MediaType.Show),
			_ => TraktId.From(value, MediaType.Show),
		};
	}

	private static MdblistIdConverter Testee(TUnit.Mocks.Http.MockHttpHandler handler) => new(MdblistApis.Titles(handler));
}
