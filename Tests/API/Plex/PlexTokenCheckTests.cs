using System.Net;
using API.Plex;
using Domain.Auth;

namespace API.Tests.Plex;

public class PlexTokenCheckTests
{
	private const string BaseUrl = "https://plex.tv";

	[Test]
	public async Task CheckAsync_ForATokenPlexAccepts_IsValidAndSendsAToken()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var testee = new PlexTokenCheck(new SingleClientHttpClientFactory(handler.CreateClient(BaseUrl)));

		await Assert.That(await testee.CheckAsync(Guid.NewGuid(), "plex-token")).IsEqualTo(AccessCheck.Valid);
		handler.Verify(request => request.HasHeader("X-Plex-Token"), Times.Once);
	}

	[Test]
	public async Task CheckAsync_ForATokenPlexRefuses_IsRejected()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Unauthorized);
		var testee = new PlexTokenCheck(new SingleClientHttpClientFactory(handler.CreateClient(BaseUrl)));

		await Assert.That(await testee.CheckAsync(Guid.NewGuid(), "plex-token")).IsEqualTo(AccessCheck.Rejected);
	}
}
