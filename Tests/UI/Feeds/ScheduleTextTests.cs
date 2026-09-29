using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class ScheduleTextTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 27, 14, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task Away_ReadsAsAHumanWouldSayIt()
	{
		await Assert.That(ScheduleText.Away(Now.AddMinutes(40), Now)).IsEqualTo("in 40 min");
		await Assert.That(ScheduleText.Away(Now.AddHours(7).AddMinutes(12), Now)).IsEqualTo("in 7 hr 12 min");
		await Assert.That(ScheduleText.Away(Now.AddHours(8), Now)).IsEqualTo("in 8 hr");
		await Assert.That(ScheduleText.Away(Now.AddDays(3), Now)).IsEqualTo("in 3 days");
		await Assert.That(ScheduleText.Away(Now.AddMinutes(-5), Now)).IsEqualTo("due now");
	}

	[Test]
	public async Task Ago_ReadsAsAHumanWouldSayIt()
	{
		await Assert.That(ScheduleText.Ago(Now.AddMinutes(-16), Now)).IsEqualTo("16 min ago");
		await Assert.That(ScheduleText.Ago(Now.AddHours(-2), Now)).IsEqualTo("2 hours ago");
		await Assert.That(ScheduleText.Ago(Now.AddDays(-12), Now)).IsEqualTo("12 days ago");
	}
}
