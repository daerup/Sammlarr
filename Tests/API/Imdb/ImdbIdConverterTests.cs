using API.Imdb;
using API.Tests.TMDB;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.Imdb;

public class ImdbIdConverterTests
{
	[Test]
	public async Task ConvertAsync_FromATmdbId_ReadsTheImdbIdFromTmdbsExternalIds()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(".*movie/550/external_ids.*")).RespondWithJson("{\"imdb_id\":\"tt0137523\",\"tvdb_id\":null}");
		var testee = new ImdbIdConverter(null!, TmdbApis.Over(handler));

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Tmdb), [TmdbId.From("550", MediaType.Movie)], new None());

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(ImdbId.From("tt0137523", MediaType.Movie)));
	}
}
