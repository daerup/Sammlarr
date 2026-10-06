using System.Net;
using API.Mdblist;
using Domain.Auth;
using Shared.Options;

namespace API.Tests.Mdblist;

public class MdblistKeyCheckTests
{
	private const string BaseUrl = "https://api.mdblist.com";

	[Test]
	public async Task CheckAsync_ForAKeyMdblistAccepts_IsValidAndSendsTheKeyAsQueryParameter()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var testee = new MdblistKeyCheck(new SingleClientHttpClientFactory(handler.CreateClient(BaseUrl)), new MdblistOptions { BaseUrl = BaseUrl });

		await Assert.That(await testee.CheckAsync("mdblist-key")).IsEqualTo(AccessCheck.Valid);
		handler.Verify(request => request.PathMatches(".*apikey=mdblist-key.*"), Times.Once);
	}

	[Test]
	public async Task CheckAsync_ForAKeyMdblistRefuses_IsRejected()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.Forbidden);
		var testee = new MdblistKeyCheck(new SingleClientHttpClientFactory(handler.CreateClient(BaseUrl)), new MdblistOptions { BaseUrl = BaseUrl });

		await Assert.That(await testee.CheckAsync("mdblist-key")).IsEqualTo(AccessCheck.Rejected);
	}
}
