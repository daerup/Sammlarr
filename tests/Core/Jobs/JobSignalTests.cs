using Core.Jobs;

namespace Core.Tests.Jobs;

public class JobSignalTests
{
	[Test]
	public async Task NudgesAsync_YieldsOneNudgePerRaise()
	{
		var testee = new JobSignal();
		testee.Raise();
		testee.Raise();
		testee.Complete();

		await Assert.That(await CountAsync(testee)).IsEqualTo(2);
	}

	[Test]
	public async Task NudgesAsync_WhenNothingIsRaised_YieldsNothing()
	{
		var testee = new JobSignal();
		testee.Complete();

		await Assert.That(await CountAsync(testee)).IsEqualTo(0);
	}

	[Test]
	public async Task NudgesAsync_WhenRaisedWhileWaiting_YieldsWithoutPolling()
	{
		var testee = new JobSignal();
		var reading = CountAsync(testee);

		testee.Raise();
		testee.Complete();

		await Assert.That(await reading.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo(1);
	}

	[Test]
	public async Task Raise_AfterCompletion_IsIgnoredRatherThanThrowing()
	{
		var testee = new JobSignal();
		testee.Complete();

		await Assert.That(testee.Raise).ThrowsNothing();
	}

	private static async Task<int> CountAsync(JobSignal signal)
	{
		var nudges = 0;
		await foreach (var _ in signal.NudgesAsync())
		{
			nudges++;
		}

		return nudges;
	}
}
