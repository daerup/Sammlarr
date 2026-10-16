using Domain.Media;
using TestInfrastructure;

namespace API.Tests.TMDB;

public class TmdbApiTests
{
	private static readonly TmdbId FightClub = TmdbId.From("550", MediaType.Movie);

	[Test]
	public async Task GetDetailWithExternalIdsAsync_AskedTwiceForOneId_SendsOneRequest()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"id":550,"title":"Fight Club","external_ids":{"imdb_id":"tt0137523","tvdb_id":null}}""");
		var testee = TmdbApis.Over(handler);

		await testee.GetDetailWithExternalIdsAsync(FightClub);
		var second = await testee.GetDetailWithExternalIdsAsync(FightClub);

		await Assert.That(second).IsSuccess();
		handler.Verify(request => request.Method(HttpMethod.Get), Times.Once);
	}
}
