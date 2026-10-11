using System.Net;
using API.Http;
using Domain.Auth;
using Polly.Timeout;
using Shared.Concurrency;

namespace API.Tests.Http;

public class AccessCheckExtensionsTests
{
	private const string Endpoint = "https://check.test/user";

	public static IEnumerable<Func<Exception>> Failures()
	{
		yield return () => new HttpRequestException("network");
		yield return () => new TimeoutRejectedException();
		yield return () => new LaneRefusedException("blocked until 2026-10-11T12:05:00Z");
	}

	[Test]
	[Arguments(HttpStatusCode.OK, AccessCheck.Valid)]
	[Arguments(HttpStatusCode.Unauthorized, AccessCheck.Rejected)]
	[Arguments(HttpStatusCode.Forbidden, AccessCheck.Rejected)]
	[Arguments(HttpStatusCode.TooManyRequests, AccessCheck.Spent)]
	[Arguments(HttpStatusCode.ServiceUnavailable, AccessCheck.Unreachable)]
	[Arguments(HttpStatusCode.BadGateway, AccessCheck.Unreachable)]
	public async Task CheckAccessAsync_ForAnAnswer_IsWhatItsStatusSays(HttpStatusCode status, AccessCheck expected)
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(status);
		using var client = new HttpClient(handler);

		await Assert.That(await client.CheckAccessAsync(new HttpRequestMessage(HttpMethod.Get, Endpoint), CancellationToken.None)).IsEqualTo(expected);
	}

	[Test]
	[MethodDataSource(nameof(Failures))]
	public async Task CheckAccessAsync_WhenNoAnswerArrives_IsUnreachable(Exception failure)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(failure);
		using var client = new HttpClient(handler);

		await Assert
			.That(await client.CheckAccessAsync(new HttpRequestMessage(HttpMethod.Get, Endpoint), CancellationToken.None))
			.IsEqualTo(AccessCheck.Unreachable);
	}
}
