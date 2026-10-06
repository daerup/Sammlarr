using System.Net;
using API.Tmdb;
using Domain.Auth;
using Shared.Options;

namespace API.Tests.TMDB;

public class TmdbKeyCheckTests
{
	private static readonly TmdbOptions Options = new() { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p" };

	[Test]
	public async Task CheckAsync_ForAKeyTmdbAccepts_IsValidAndSendsTheKeyAsQueryParameter()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var testee = new TmdbKeyCheck(new SingleClientHttpClientFactory(handler.CreateClient(Options.BaseUrl)), Options);

		await Assert.That(await testee.CheckAsync("tmdb-key")).IsEqualTo(AccessCheck.Valid);
		handler.Verify(request => request.PathMatches(".*api_key=tmdb-key.*"), Times.Once);
	}

	[Test]
	public async Task CheckAsync_ForAKeyTmdbRefuses_IsRejected()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		var testee = new TmdbKeyCheck(new SingleClientHttpClientFactory(handler.CreateClient(Options.BaseUrl)), Options);

		await Assert.That(await testee.CheckAsync("tmdb-key")).IsEqualTo(AccessCheck.Rejected);
	}
}
