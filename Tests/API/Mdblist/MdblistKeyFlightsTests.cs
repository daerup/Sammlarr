using API.Mdblist;
using Domain.Auth;

namespace API.Tests.Mdblist;

public class MdblistKeyFlightsTests
{
	private MdblistKeyFlights Testee { get; } = new();

	[Test]
	public async Task JoinAsync_ForOneKeyCheckedTwiceAtOnce_ChecksOnceAndAnswersBoth()
	{
		var answer = new TaskCompletionSource<AccessCheck>();
		var checks = 0;

		var first = Testee.JoinAsync("mdblist-key", () => Checked(ref checks, answer.Task));
		var second = Testee.JoinAsync("mdblist-key", () => Checked(ref checks, answer.Task));
		answer.SetResult(AccessCheck.Valid);

		await Assert.That(await Task.WhenAll(first, second)).IsEquivalentTo([AccessCheck.Valid, AccessCheck.Valid]);
		await Assert.That(checks).IsEqualTo(1);
	}

	[Test]
	public async Task JoinAsync_AfterACheckSettled_ChecksAgain()
	{
		var checks = 0;

		await Testee.JoinAsync("mdblist-key", () => Checked(ref checks, Task.FromResult(AccessCheck.Unreachable)));
		var again = await Testee.JoinAsync("mdblist-key", () => Checked(ref checks, Task.FromResult(AccessCheck.Valid)));

		await Assert.That(again).IsEqualTo(AccessCheck.Valid);
		await Assert.That(checks).IsEqualTo(2);
	}

	[Test]
	public async Task JoinAsync_ForTwoKeysAtOnce_ChecksEach()
	{
		var answer = new TaskCompletionSource<AccessCheck>();
		var checks = 0;

		var first = Testee.JoinAsync("first-key", () => Checked(ref checks, answer.Task));
		var second = Testee.JoinAsync("second-key", () => Checked(ref checks, answer.Task));
		answer.SetResult(AccessCheck.Valid);
		await Task.WhenAll(first, second);

		await Assert.That(checks).IsEqualTo(2);
	}

	private static Task<AccessCheck> Checked(ref int checks, Task<AccessCheck> answer)
	{
		Interlocked.Increment(ref checks);
		return answer;
	}
}
