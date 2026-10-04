using Core.Auth;
using Domain.Identity;

namespace Core.Tests.Auth;

public class SignInGateTests
{
	private readonly SignInGate _testee = new();

	[Test]
	public async Task EnterAsync_WhileTheSameAccountIsBeingChecked_WaitsForItsTurn()
	{
		var id = UserId.New();
		var first = await _testee.EnterAsync(id);

		var second = _testee.EnterAsync(id);
		var waited = !second.IsCompleted;
		first.Dispose();
		(await second).Dispose();

		await Assert.That(waited).IsTrue();
	}

	[Test]
	public async Task EnterAsync_ForAnotherAccount_DoesNotWait()
	{
		using var first = await _testee.EnterAsync(UserId.New());

		var other = _testee.EnterAsync(UserId.New());

		await Assert.That(other.IsCompleted).IsTrue();
		(await other).Dispose();
	}
}
