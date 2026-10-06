using Domain.Providers;

namespace Domain.Tests.Providers;

public class RemoteLoadTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan Budget = TimeSpan.FromSeconds(15);

	[Test]
	public async Task FreesWithin_WhenOpen_IsTrueEvenWithoutABudget()
	{
		await Assert.That(new RemoteLoad(new None(), 0).FreesWithin(Now, TimeSpan.Zero)).IsTrue();
	}

	[Test]
	public async Task FreesWithin_WhenTheBlockEndsInsideTheBudget_IsTrue()
	{
		await Assert.That(new RemoteLoad(Some.Of(Now.Add(Budget)), 0).FreesWithin(Now, Budget)).IsTrue();
	}

	[Test]
	public async Task FreesWithin_WhenTheBlockOutlastsTheBudget_IsFalse()
	{
		await Assert.That(new RemoteLoad(Some.Of(Now.Add(Budget).AddSeconds(1)), 0).FreesWithin(Now, Budget)).IsFalse();
	}

	[Test]
	public async Task FreeAt_IsTheEndOfTheBlockOrNowWhenOpen()
	{
		await Assert.That(new RemoteLoad(Some.Of(Now.AddSeconds(5)), 0).FreeAt(Now)).IsEqualTo(Now.AddSeconds(5));
		await Assert.That(new RemoteLoad(new None(), 0).FreeAt(Now)).IsEqualTo(Now);
	}
}
