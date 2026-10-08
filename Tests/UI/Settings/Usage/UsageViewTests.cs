using System.Globalization;
using Domain.Usage;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageViewTests
{
	[Test]
	[Arguments(5, "≤5 ms")]
	[Arguments(250, "≤250 ms")]
	[Arguments(1000, "≤1 s")]
	[Arguments(2500, "≤2.5 s")]
	[Arguments(DurationBuckets.Over, ">10 s")]
	public async Task Bucket_ByItsUpperBound_IsLabelledUpToIt(int upToMs, string label)
	{
		await Assert.That(UsageView.Bucket(upToMs)).IsEqualTo(label);
	}

	[Test]
	[Arguments(1L, "1 call")]
	[Arguments(0L, "0 calls")]
	[Arguments(8287L, "8,287 calls")]
	public async Task Calls_ByCount_IsSingularForOne(long count, string text)
	{
		await Assert.That(UsageView.Calls(count)).IsEqualTo(text);
	}

	[Test]
	public async Task RightNow_PausedWhileFailing_SaysPausedFirst()
	{
		var at = new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero);

		var now = UsageView.RightNow(UsageSamples.Quiet with { PausedUntil = Some.Of(at), FailingSince = Some.Of(at) }, at);

		await Assert.That(now.Tone).IsEqualTo(RightNowTone.Paused);
		await Assert.That(now.Text).StartsWith("Paused until");
	}

	[Test]
	public async Task Clock_OnAnotherDay_NamesTheDay()
	{
		var at = new DateTimeOffset(2026, 10, 3, 4, 30, 0, TimeSpan.Zero);

		await Assert.That(UsageView.Clock(at, at.AddDays(4))).IsEqualTo(at.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture));
		await Assert.That(UsageView.Clock(at, at.AddMinutes(5))).IsEqualTo(at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture));
	}
}
