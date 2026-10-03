using System.Net;
using API.Tmdb;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.TMDB;

public class TmdbIdConverterTests
{
	private static readonly TmdbId FightClub = TmdbId.From("550", MediaType.Movie);
	private static readonly TmdbId BreakingBad = TmdbId.From("1396", MediaType.Show);

	[Test]
	public async Task LookUpAsync_FindByAnImdbId_FindsTheTmdbId()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(".*find/tt0137523.*external_source=imdb_id.*"))
			.RespondWithJson("{\"movie_results\":[{\"id\":550}],\"tv_results\":[]}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0137523", MediaType.Movie)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).Contains(FightClub));
	}

	[Test]
	public async Task LookUpAsync_FindByAnImdbId_HintsAtTheOriginalTitleAndItsReleaseYear()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson("{\"movie_results\":[{\"id\":550,\"original_title\":\"Fight Club\",\"release_date\":\"1999-10-15\"}],\"tv_results\":[]}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0137523", MediaType.Movie)));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
				await Assert
					.That(found.Hints)
					.IsSome()
					.And.IsEqualTo(
						new IdentifyHints
						{
							Title = "Fight Club",
							Year = 1999,
							MediaType = MediaType.Movie,
						}
					)
			);
	}

	[Test]
	public async Task LookUpAsync_FindForAnUnreleasedTitle_HintsAtYearZero()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{\"movie_results\":[{\"id\":550,\"original_title\":\"Fight Club\",\"release_date\":\"\"}],\"tv_results\":[]}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0137523", MediaType.Movie)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Hints.Map(hints => hints.Year)).IsSome().And.IsEqualTo(0));
	}

	[Test]
	public async Task LookUpAsync_DetailOfAShow_HintsAtTheOriginalNameAndItsFirstAirYear()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"{\"id\":1396,\"original_name\":\"Breaking Bad\",\"first_air_date\":\"2008-01-20\",\"external_ids\":{\"imdb_id\":null,\"tvdb_id\":81189}}"
			);
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert
			.That(result)
			.IsSuccess()
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
	public async Task LookUpAsync_FindByATvdbId_FindsTheTmdbShow()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(".*find/81189.*external_source=tvdb_id.*"))
			.RespondWithJson("{\"movie_results\":[],\"tv_results\":[{\"id\":1396,\"original_name\":\"Breaking Bad\"}]}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(TvdbId.From("81189", MediaType.Show)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).Contains(BreakingBad));
	}

	[Test]
	public async Task LookUpAsync_FindByATvdbIdWhileTmdbIsRateLimited_FailsAsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(TvdbId.From("81189", MediaType.Show)));

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsTypeOf<ProviderUnavailableException>());
	}

	[Test]
	public async Task LookUpAsync_DetailOfAMovie_ReadsTheImdbIdFromTheAppendedExternalIds()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(".*movie/550.*append_to_response=external_ids.*"))
			.RespondWithJson("{\"id\":550,\"original_title\":\"Fight Club\",\"external_ids\":{\"imdb_id\":\"tt0137523\",\"tvdb_id\":null}}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(FightClub));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).Contains(ImdbId.From("tt0137523", MediaType.Movie)));
	}

	[Test]
	public async Task LookUpAsync_DetailOfAShow_ReadsTheTvdbIdFromTheAppendedExternalIds()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(".*tv/1396.*append_to_response=external_ids.*"))
			.RespondWithJson("{\"id\":1396,\"original_name\":\"Breaking Bad\",\"external_ids\":{\"imdb_id\":\"tt0903747\",\"tvdb_id\":81189}}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).Contains(TvdbId.From("81189", MediaType.Show)));
	}

	[Test]
	public async Task LookUpAsync_DetailWhenTmdbKnowsNoTvdbId_SucceedsWithoutOne()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{\"id\":1396,\"original_name\":\"Breaking Bad\",\"external_ids\":{\"imdb_id\":null,\"tvdb_id\":null}}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}

	[Test]
	public async Task LookUpAsync_DetailWithoutAnOriginalTitle_ReturnsItsIdsWithoutHints()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{\"id\":1396,\"external_ids\":{\"imdb_id\":null,\"tvdb_id\":81189}}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids).Contains(TvdbId.From("81189", MediaType.Show));
				await Assert.That(found.Hints).IsNone();
			});
	}

	[Test]
	public async Task LookUpAsync_DetailWhileTmdbIsRateLimited_FailsAsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsTypeOf<ProviderUnavailableException>());
	}

	[Test]
	public async Task LookUpAsync_Detail_DropsABlankImdbIdAndATvdbIdOfZero()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{\"id\":1396,\"original_name\":\"Breaking Bad\",\"external_ids\":{\"imdb_id\":\" \",\"tvdb_id\":0}}");
		var testee = new TmdbIdConverter(TmdbApis.Over(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}
}
