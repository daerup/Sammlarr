using System.Net;
using API.Tests.TMDB;
using API.TvDB;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.TvDB;

public class TvdbIdConverterTests
{
	private static readonly TmdbId BreakingBad = TmdbId.From("1396", MediaType.Show);

	[Test]
	public async Task ConvertAsync_FromATmdbId_ReadsTheTvdbIdFromTmdbsExternalIds()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(".*tv/1396/external_ids.*")).RespondWithJson("{\"imdb_id\":\"tt0903747\",\"tvdb_id\":81189}");
		var testee = new TvdbIdConverter(null!, TmdbApis.Over(handler));

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Tmdb), [BreakingBad], new None());

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(TvdbId.From("81189", MediaType.Show)));
	}

	[Test]
	public async Task ConvertAsync_WhenTmdbKnowsNoTvdbId_FailsAsAnAnswer()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{\"imdb_id\":null,\"tvdb_id\":null}");
		var testee = new TvdbIdConverter(null!, TmdbApis.Over(handler));

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Tmdb), [BreakingBad], new None());

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsNull());
	}

	[Test]
	public async Task ConvertAsync_FromATmdbIdWhileTmdbIsRateLimited_FailsAsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);
		var testee = new TvdbIdConverter(null!, TmdbApis.Over(handler));

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Tmdb), [BreakingBad], new None());

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsTypeOf<ProviderUnavailableException>());
	}
}
