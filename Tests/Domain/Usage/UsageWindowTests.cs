using Domain.Usage;

namespace Domain.Tests.Usage;

public class UsageWindowTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 30, 0, TimeSpan.FromHours(2));

	[Test]
	public async Task Of_ADay_IsTwentyFourHoursEndingWithTheCurrentOne()
	{
		var window = UsageWindow.Of(UsageRange.Day, Now);

		await Assert.That(window).IsEqualTo(new UsageWindow(new(2026, 10, 6, 13, 0, 0, Now.Offset), new(2026, 10, 7, 13, 0, 0, Now.Offset), TimeSpan.FromHours(1)));
		await Assert.That(window.Slots).IsEqualTo(24);
	}

	[Test]
	public async Task Of_AWeek_IsSevenLocalDaysEndingWithToday()
	{
		var window = UsageWindow.Of(UsageRange.Week, Now);

		await Assert.That(window).IsEqualTo(new UsageWindow(new(2026, 10, 1, 0, 0, 0, Now.Offset), new(2026, 10, 8, 0, 0, 0, Now.Offset), TimeSpan.FromDays(1)));
		await Assert.That(window.SlotOf(new DateTimeOffset(2026, 10, 7, 21, 0, 0, TimeSpan.Zero))).IsEqualTo(6);
	}

	[Test]
	public async Task Before_AMonth_IsTheThirtyDaysBeforeIt()
	{
		var window = UsageWindow.Of(UsageRange.Month, Now);

		var before = window.Before();

		await Assert.That(before.To).IsEqualTo(window.From);
		await Assert.That(before.Slots).IsEqualTo(30);
		await Assert.That(before.Contains(window.From)).IsFalse();
	}
}
