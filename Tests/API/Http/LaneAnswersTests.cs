using System.Text.Json;
using API.Http;
using API.Http.Lanes;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.Http;

public class LaneAnswersTests
{
	[Test]
	public async Task ToAnswer_ForAHit_IsFound()
	{
		var answer = ((LaneAnswer<string>)new LaneHit<string>("tt0137523")).ToAnswer(RemoteService.Mdblist);

		await Assert.That(answer).IsFound().And.IsEqualTo("tt0137523");
	}

	[Test]
	public async Task ToAnswer_ForAMiss_IsNoMatch()
	{
		var answer = ((LaneAnswer<string>)new LaneMiss()).ToAnswer(RemoteService.Mdblist);

		await Assert.That(answer).IsOfUnionType<Answer<string>, NoMatch>().And.IsEqualTo(new NoMatch(RemoteService.Mdblist));
	}

	[Test]
	public async Task ToAnswer_ForARefusalBeforeSending_IsUnavailableAndNotSent()
	{
		var answer = Failed(new RemoteUnavailableException(RemoteService.Mdblist, sent: false));

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Mdblist, new None(), false));
	}

	[Test]
	public async Task ToAnswer_ForABrokenConnection_IsUnavailableAndSent()
	{
		var answer = Failed(new HttpRequestException("The connection was reset."));

		await Assert.That(answer).IsOfUnionType<Answer<string>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Mdblist, new None(), true));
	}

	[Test]
	public async Task ToAnswer_ForAnAnswerThatCannotBeRead_IsFaulted()
	{
		var answer = Failed(new JsonException("'<' is an invalid start of a value."));

		await Assert.That(answer).IsOfUnionType<Answer<string>, Faulted>();
	}

	[Test]
	public async Task ToAnswer_ForAFailureWithoutACause_IsFaulted()
	{
		var answer = ((LaneAnswer<string>)new LaneFailed(Error.From("the batch was cancelled"))).ToAnswer(RemoteService.Mdblist);

		await Assert.That(answer).IsOfUnionType<Answer<string>, Faulted>();
	}

	private static Answer<string> Failed(Exception cause) => ((LaneAnswer<string>)new LaneFailed(Error.From(cause))).ToAnswer(RemoteService.Mdblist);
}
