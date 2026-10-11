using System.Text.Json;
using API.Http;
using Domain.Auth;
using Domain.Providers;
using Polly.Timeout;
using Shared.Concurrency;
using TestInfrastructure;

namespace API.Tests.Http;

public class AnsweringTests
{
	private static readonly DateTimeOffset Reset = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task GuardedAsync_ForAValue_IsFound()
	{
		var answer = await Task.FromResult<Result<string>>(Success.Of("tt0137523")).GuardedAsync(RemoteService.Tmdb);

		await Assert.That(answer).IsFound().And.IsEqualTo("tt0137523");
	}

	[Test]
	public async Task GuardedAsync_ForAnErrorWithoutACause_IsNoMatch()
	{
		var answer = await Task.FromResult<Result<string>>(Error.From("TMDB knows no such title.")).GuardedAsync(RemoteService.Tmdb);

		await Assert.That(answer).IsOfUnionType<Answer<string>, NoMatch>().And.IsEqualTo(new NoMatch(RemoteService.Tmdb));
	}

	[Test]
	public async Task GuardedAsync_ForABlockRaisedAsRemoteUnavailable_IsUnavailableAndNotSent()
	{
		var blocked = new RemoteUnavailableException(RemoteService.Mdblist, sent: false) { Until = Some.Of(Reset) };

		var answer = await Task.FromResult<Result<string>>(Error.From("Kept for feeds.", blocked)).GuardedAsync(RemoteService.Mdblist);

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Mdblist, Some.Of(Reset), false));
	}

	[Test]
	public async Task GuardedAsync_ForAServerErrorRaisedAsRemoteUnavailable_IsUnavailableAndSent()
	{
		var answer = await Thrown(new RemoteUnavailableException(RemoteService.Plex, sent: true));

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Plex, new None(), true));
	}

	[Test]
	public async Task GuardedAsync_ForAnHttpRequestException_IsUnavailableAndSent()
	{
		var answer = await Thrown(new HttpRequestException("The connection was reset."));

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Plex, new None(), true));
	}

	[Test]
	public async Task GuardedAsync_ForATimeout_IsUnavailableAndSent()
	{
		var answer = await Thrown(new TimeoutRejectedException());

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Plex, new None(), true));
	}

	[Test]
	public async Task GuardedAsync_ForAnHttpClientTimeout_IsUnavailableAndSent()
	{
		var answer = await Thrown(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException()));

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Plex, new None(), true));
	}

	[Test]
	public async Task GuardedAsync_ForALaneRefusal_IsUnavailableAndNotSent()
	{
		var answer = await Thrown(new LaneRefusedException("blocked until 2026-10-11T12:05:00Z"));

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Plex, new None(), false));
	}

	[Test]
	public async Task GuardedAsync_ForARejectedKey_IsUnavailableAndNotSent()
	{
		var rejected = new SecretRejectedException("The stored Tmdb secret was rejected.");

		var answer = await Task.FromResult<Result<string>>(Error.From(rejected.Message, rejected)).GuardedAsync(RemoteService.Tmdb);

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Tmdb, new None(), false));
	}

	[Test]
	public async Task GuardedAsync_ForAnAnswerThatCannotBeRead_IsFaulted()
	{
		var unreadable = new JsonException("'<' is an invalid start of a value.");

		var answer = await Thrown(unreadable);

		var faulted = await Assert.That(answer).IsOfUnionType<Answer<string>, Faulted>();
		await Assert.That(faulted!.Error.Exception).IsSome().And.IsSameReferenceAs(unreadable);
	}

	[Test]
	public async Task GuardedAsync_WhenCancelled_Rethrows()
	{
		await Assert.That(async () => await Thrown(new OperationCanceledException())).Throws<OperationCanceledException>();
	}

	private static Task<Answer<string>> Thrown(Exception exception) => Task.FromException<Result<string>>(exception).GuardedAsync(RemoteService.Plex);
}
