using System.Net;
using System.Text;
using API.Trakt;
using Domain.Auth;
using Shared.Options;
using TUnit.Mocks.Http;

namespace API.Tests.Trakt;

public class TraktClientIdCheckTests
{
	[Test]
	public async Task CheckAsync_ForAClientIdTraktTakes_IsValid()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""[{"name":"Action","slug":"action"}]""");

		await Assert.That(await Testee(handler).CheckAsync("trakt-client-id")).IsEqualTo(AccessCheck.Valid);
		handler.Verify(request => request.PathMatches("^/genres/movies$").Header("trakt-api-key", "trakt-client-id"), Times.Once);
	}

	[Test]
	public async Task CheckAsync_ForTraktsOwnForbidden_IsRejected()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"error":"invalid api key"}""", HttpStatusCode.Forbidden);

		await Assert.That(await Testee(handler).CheckAsync("wrong")).IsEqualTo(AccessCheck.Rejected);
	}

	[Test]
	public async Task CheckAsync_ForCloudflaresForbiddenPage_IsUnreachable()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("<html>blocked</html>", Encoding.UTF8, "text/html") });

		await Assert.That(await Testee(handler).CheckAsync("trakt-client-id")).IsEqualTo(AccessCheck.Unreachable);
	}

	[Test]
	public async Task CheckAsync_WhenTraktLimitsRequests_IsSpent()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);

		await Assert.That(await Testee(handler).CheckAsync("trakt-client-id")).IsEqualTo(AccessCheck.Spent);
	}

	private static TraktClientIdCheck Testee(MockHttpHandler handler)
	{
		var clients = IHttpClientFactory.Mock();
		clients.CreateClient(TraktClient.Unauthenticated).Returns(handler.CreateClient("https://api.trakt.tv/"));
		return new TraktClientIdCheck(clients.Object, new TraktOptions { BaseUrl = "https://api.trakt.tv", UserAgent = "Sammlarr-tests" });
	}
}
