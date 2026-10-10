using API.Http;
using API.Tests.Feeds;
using Contracts;
using TestInfrastructure;

namespace API.Tests.Http;

public class RecentAnswersTests
{
	[Test]
	public async Task AskAsync_WhileTheFirstAskForAKeyRuns_SharesItsAnswer()
	{
		var asked = 0;
		var answer = new TaskCompletionSource<Result<string>>();
		var ask = () =>
		{
			asked++;
			return answer.Task;
		};
		var testee = Testee();
		var first = testee.AskAsync("dune", ask);
		var second = testee.AskAsync("dune", ask);

		answer.SetResult(Success.Of("Dune"));

		await Assert.That(await first).IsSuccess().And.IsEqualTo("Dune");
		await Assert.That(await second).IsSuccess().And.IsEqualTo("Dune");
		await Assert.That(asked).IsEqualTo(1);
	}

	[Test]
	[Arguments(599, 1)]
	[Arguments(600, 2)]
	public async Task AskAsync_SomeSecondsAfterAnAnswer_AsksAgainOnceTenMinutesHavePassed(int seconds, int asks)
	{
		var asked = 0;
		var now = FeedClock.Now;
		var ask = () =>
		{
			asked++;
			return Task.FromResult<Result<string>>(Success.Of("Dune"));
		};
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(() => now);
		var testee = new RecentAnswers<string, string>(clock.Object);
		await testee.AskAsync("dune", ask);
		now = now.AddSeconds(seconds);

		await testee.AskAsync("dune", ask);

		await Assert.That(asked).IsEqualTo(asks);
	}

	[Test]
	public async Task AskAsync_AfterAFailedAnswer_AsksAgain()
	{
		var asked = 0;
		var ask = () => Task.FromResult<Result<string>>(++asked == 1 ? Error.From("Plex is down.") : Success.Of("Dune"));
		var testee = Testee();
		await testee.AskAsync("dune", ask);

		var second = await testee.AskAsync("dune", ask);

		await Assert.That(second).IsSuccess().And.IsEqualTo("Dune");
		await Assert.That(asked).IsEqualTo(2);
	}

	[Test]
	public async Task AskAsync_AfterAnAskThatThrew_AsksAgain()
	{
		var asked = 0;
		var ask = () =>
			++asked == 1 ? Task.FromException<Result<string>>(new HttpRequestException("Plex is down.")) : Task.FromResult<Result<string>>(Success.Of("Dune"));
		var testee = Testee();
		await Assert.That(async () => await testee.AskAsync("dune", ask)).Throws<HttpRequestException>();

		var second = await testee.AskAsync("dune", ask);

		await Assert.That(second).IsSuccess().And.IsEqualTo("Dune");
	}

	[Test]
	public async Task AskAsync_AfterAnAskThatThrewBeforeItsFirstAwait_AsksAgain()
	{
		var asked = 0;
		var ask = () => ++asked == 1 ? throw new HttpRequestException("Plex is down.") : Task.FromResult<Result<string>>(Success.Of("Dune"));
		var testee = Testee();
		await Assert.That(async () => await testee.AskAsync("dune", ask)).Throws<HttpRequestException>();

		var second = await testee.AskAsync("dune", ask);

		await Assert.That(second).IsSuccess().And.IsEqualTo("Dune");
	}

	[Test]
	public async Task AskAsync_WhenAnAskFailsAfterItsKeyWasRemembered_KeepsTheRememberedValue()
	{
		var answer = new TaskCompletionSource<Result<string>>();
		var testee = Testee();
		var failing = testee.AskAsync("tt1160419", () => answer.Task);
		testee.Remember("tt1160419", "Dune");
		answer.SetResult(Error.From("Trakt is down."));
		await failing;

		var remembered = await testee.AskAsync("tt1160419", () => throw new InvalidOperationException("Asked although the answer was remembered."));

		await Assert.That(remembered).IsSuccess().And.IsEqualTo("Dune");
	}

	[Test]
	public async Task AskAsync_WhenOneWaiterCancels_TheOthersStillGetTheAnswer()
	{
		var answer = new TaskCompletionSource<Result<string>>();
		using var cancellation = new CancellationTokenSource();
		var testee = Testee();
		var cancelled = testee.AskAsync("dune", () => answer.Task, cancellation.Token);
		var waiting = testee.AskAsync("dune", () => answer.Task);

		await cancellation.CancelAsync();
		answer.SetResult(Success.Of("Dune"));

		await Assert.That(async () => await cancelled).Throws<OperationCanceledException>();
		await Assert.That(await waiting).IsSuccess().And.IsEqualTo("Dune");
	}

	[Test]
	public async Task AskAsync_ForAKeyAValueWasRememberedUnder_AnswersWithoutAsking()
	{
		var testee = Testee();
		testee.Remember("tt1160419", "Dune");

		var answer = await testee.AskAsync("tt1160419", () => throw new InvalidOperationException("Asked although the answer was remembered."));

		await Assert.That(answer).IsSuccess().And.IsEqualTo("Dune");
	}

	private static RecentAnswers<string, string> Testee() => new(FeedClock.Today());
}
