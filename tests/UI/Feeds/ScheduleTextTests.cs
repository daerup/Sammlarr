using System.Globalization;
using Domain.Feed;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class ScheduleTextTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 27, 14, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task Away_ReadsAsAHumanWouldSayIt()
	{
		await Assert.That(ScheduleText.Away(Now.AddMinutes(40), Now)).IsEqualTo("40 minutes from now");
		await Assert.That(ScheduleText.Away(Now.AddHours(8), Now)).IsEqualTo("8 hours from now");
		await Assert.That(ScheduleText.Away(Now.AddDays(3), Now)).IsEqualTo("3 days from now");
		await Assert.That(ScheduleText.Away(Now.AddHours(7).AddMinutes(12), Now)).IsEqualTo("7 hours from now");
		await Assert.That(ScheduleText.Away(Now.AddSeconds(115), Now)).IsEqualTo("a minute from now");
		await Assert.That(ScheduleText.Away(Now.AddMilliseconds(400), Now)).IsEqualTo("due now");
		await Assert.That(ScheduleText.Away(Now.AddMinutes(-5), Now)).IsEqualTo("due now");
	}

	[Test]
	public async Task AwayExactly_ReadsTheTimeLeftToTheSecond()
	{
		await Assert.That(ScheduleText.AwayExactly(Now.AddSeconds(115), Now)).IsEqualTo("in 1 min 55 s");
		await Assert.That(ScheduleText.AwayExactly(Now.AddSeconds(42), Now)).IsEqualTo("in 42 s");
		await Assert.That(ScheduleText.AwayExactly(Now.AddHours(1).AddSeconds(5), Now)).IsEqualTo("in 1 h 5 s");
		await Assert.That(ScheduleText.AwayExactly(Now.AddDays(2).AddHours(3).AddMinutes(4).AddSeconds(5), Now)).IsEqualTo("in 2 d 3 h 4 min 5 s");
		await Assert.That(ScheduleText.AwayExactly(Now.AddMilliseconds(400), Now)).IsEqualTo("due now");
		await Assert.That(ScheduleText.AwayExactly(Now.AddSeconds(-3), Now)).IsEqualTo("due now");
	}

	[Test]
	public async Task Describe_InAGermanBrowser_StaysEnglishLikeTheRestOfTheUi()
	{
		CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-CH");

		var text = ScheduleText.Describe(new FeedRunTrigger(new Scheduled("0 0 3 * * 1")));

		await Assert.That(text).Contains("only on Monday").And.DoesNotContain("Montag");
	}
}
