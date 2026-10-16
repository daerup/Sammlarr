using System.Net;
using API.Mdblist;

namespace API.Tests.Mdblist;

public class MdblistAnswerCheckTests
{
	[Test]
	public async Task SendAsync_ForA200WithAnErrorBody_Answers502AndLogsIt()
	{
		var logger = new CountingLogger<MdblistAnswerCheck>();

		using var response = await SendAsync("""{"error":"Invalid API key!"}""", logger);

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
		await Assert.That(logger.Warnings).IsEqualTo(1);
	}

	[Test]
	public async Task SendAsync_ForA200WhoseErrorNamesALimit_Answers429()
	{
		using var response = await SendAsync("""{"error":"API limit reached!"}""", new CountingLogger<MdblistAnswerCheck>());

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
	}

	[Test]
	public async Task SendAsync_ForA200ThatIsNotJson_Answers502()
	{
		using var response = await SendAsync("<html>maintenance</html>", new CountingLogger<MdblistAnswerCheck>());

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
	}

	[Test]
	public async Task SendAsync_ForA200WithTitles_PassesItThroughUntouched()
	{
		var logger = new CountingLogger<MdblistAnswerCheck>();

		using var response = await SendAsync("""[{"ids":{"imdb":"tt0137523"}}]""", logger);

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("""[{"ids":{"imdb":"tt0137523"}}]""");
		await Assert.That(logger.Warnings).IsEqualTo(0);
	}

	[Test]
	public async Task SendAsync_ForA200ThatSaysResponseFalse_Answers502()
	{
		using var response = await SendAsync("""{"response":false}""", new CountingLogger<MdblistAnswerCheck>());

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
	}

	[Test]
	public async Task SendAsync_ForA200WithANullError_PassesItThrough()
	{
		using var response = await SendAsync("""{"error":null,"movies":[]}""", new CountingLogger<MdblistAnswerCheck>());

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
	}

	[Test]
	public async Task SendAsync_ForARefusedAnswer_KeepsItsRateLimitHeaders()
	{
		using var response = await SendAsync("""{"error":"API limit reached!"}""", new CountingLogger<MdblistAnswerCheck>(), remaining: "0");

		await Assert.That(response.Headers.GetValues("x-ratelimit-remaining")).IsEquivalentTo(["0"]);
	}

	private static async Task<HttpResponseMessage> SendAsync(string body, CountingLogger<MdblistAnswerCheck> logger, string? remaining = null)
	{
		var wire = Mock.HttpHandler();
		var answered = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
		Optional.Of(remaining).Apply(left => answered.Headers.Add("x-ratelimit-remaining", left));
		wire.OnAnyRequest().RespondWith(answered);
		using var invoker = new HttpMessageInvoker(new MdblistAnswerCheck(logger) { InnerHandler = wire });
		using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mdblist.com/imdb/movie");
		return await invoker.SendAsync(request, CancellationToken.None);
	}
}
