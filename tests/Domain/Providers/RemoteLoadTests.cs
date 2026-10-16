using Domain.Providers;

namespace Domain.Tests.Providers;

public class RemoteLoadTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan Budget = TimeSpan.FromSeconds(15);

	[Test]
	public async Task Takes_WhenOpen_IsTrueEvenWithoutABudget()
	{
		await Assert.That(RemoteLoad.Idle.Takes(Now, TimeSpan.Zero)).IsTrue();
	}

	[Test]
	public async Task Takes_WhenTheBlockEndsInsideTheBudget_IsTrue()
	{
		await Assert.That(Blocked(Now.Add(Budget)).Takes(Now, Budget)).IsTrue();
	}

	[Test]
	public async Task Takes_WhenTheBlockOutlastsTheBudget_IsFalse()
	{
		await Assert.That(Blocked(Now.Add(Budget).AddSeconds(1)).Takes(Now, Budget)).IsFalse();
	}

	[Test]
	public async Task Takes_WhenDown_IsFalseWhateverTheBudget()
	{
		await Assert.That((RemoteLoad.Idle with { Down = true }).Takes(Now, TimeSpan.FromDays(1))).IsFalse();
	}

	[Test]
	public async Task IsOpen_OnceTheBlockEnds_IsTrue()
	{
		await Assert.That(Blocked(Now.AddSeconds(5)).IsOpen(Now)).IsFalse();
		await Assert.That(Blocked(Now.AddSeconds(5)).IsOpen(Now.AddSeconds(5))).IsTrue();
	}

	[Test]
	public async Task Weight_ForAWaitingRemote_GrowsWithTheEstimatedWait()
	{
		await Assert.That(RemoteLoad.Idle.Weight(2)).IsEqualTo(2);
		await Assert.That((RemoteLoad.Idle with { EstimatedWait = TimeSpan.FromSeconds(10) }).Weight(2)).IsEqualTo(22);
	}

	private static RemoteLoad Blocked(DateTimeOffset until) => RemoteLoad.Idle with { BlockedUntil = Some.Of(until), EstimatedWait = until - Now };
}
