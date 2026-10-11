using Core.Media;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Media;

public class RunWatchdogTests
{
	private static readonly TimeSpan StallAfter = TimeSpan.FromMinutes(3);

	[Test]
	public async Task Check_WhenNothingSettlesForStallAfter_CancelsTheRest()
	{
		var time = new FakeTimeProvider();
		await using var testee = new RunWatchdog(time, StallAfter, CancellationToken.None);
		testee.Started();

		time.Advance(StallAfter);

		await Assert.That(testee.Token.IsCancellationRequested).IsTrue();
		await Assert.That(testee.Starved).IsTrue();
	}

	[Test]
	public async Task Check_WhileTitlesKeepSettling_NeverFires()
	{
		var time = new FakeTimeProvider();
		await using var testee = new RunWatchdog(time, StallAfter, CancellationToken.None);
		testee.Started();

		for (var minute = 0; minute < 10; minute++)
		{
			time.Advance(TimeSpan.FromMinutes(1));
			testee.Settled();
			testee.Started();
		}

		await Assert.That(testee.Token.IsCancellationRequested).IsFalse();
	}

	[Test]
	public async Task Check_AfterAnIdleGap_JudgesTheNextTitleFromItsStart()
	{
		var time = new FakeTimeProvider();
		await using var testee = new RunWatchdog(time, StallAfter, CancellationToken.None);
		testee.Started();
		testee.Settled();
		time.Advance(StallAfter * 2);

		testee.Started();
		time.Advance(TimeSpan.FromMinutes(1));

		await Assert.That(testee.Token.IsCancellationRequested).IsFalse();
	}

	[Test]
	public async Task Check_WithNoTitleOpen_NeverFires()
	{
		var time = new FakeTimeProvider();
		await using var testee = new RunWatchdog(time, StallAfter, CancellationToken.None);

		time.Advance(StallAfter * 4);

		await Assert.That(testee.Token.IsCancellationRequested).IsFalse();
	}
}
