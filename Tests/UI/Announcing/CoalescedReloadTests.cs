using UI.Shared.Announcing;

namespace UI.Tests.Announcing;

public class CoalescedReloadTests
{
	[Test]
	public async Task RunAsync_WhileAReloadIsRunning_QueuesOneMoreInsteadOfOverlapping()
	{
		var gate = new TaskCompletionSource();
		var runs = 0;
		var testee = new CoalescedReload(async () =>
		{
			runs++;
			await gate.Task;
		});

		var first = testee.RunAsync();
		await testee.RunAsync();
		await testee.RunAsync();
		gate.SetResult();
		await first;

		await Assert.That(runs).IsEqualTo(2);
	}

	[Test]
	public async Task RunAsync_WhenIdle_ReloadsOnce()
	{
		var runs = 0;
		var testee = new CoalescedReload(() =>
		{
			runs++;
			return Task.CompletedTask;
		});

		await testee.RunAsync();

		await Assert.That(runs).IsEqualTo(1);
	}
}
