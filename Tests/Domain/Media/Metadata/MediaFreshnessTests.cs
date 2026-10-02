using Domain.Media;
using Domain.Media.Metadata;

namespace Domain.Tests.Media.Metadata;

public class MediaFreshnessTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task IsFresh_WhenNeverFetched_IsStale()
	{
		IdentifiedMedia stored = MediaFakes.CompleteMovie();

		await Assert.That(MediaFreshness.IsFresh(stored, null, Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenCompleteAndFetchedWithinTheWeek_IsFresh()
	{
		IdentifiedMedia stored = MediaFakes.CompleteMovie();

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromDays(6), Now)).IsTrue();
	}

	[Test]
	public async Task IsFresh_WhenCompleteAndOlderThanTheWeek_IsStale()
	{
		IdentifiedMedia stored = MediaFakes.CompleteMovie();

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromDays(8), Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenEveryGapIsConfirmed_StaysFreshForTheWeek()
	{
		var stored = IdentifiedMedia.From(
			MediaFakes.CompletePartialMovie() with
			{
				Tagline = null,
				Summary = null,
				ConfirmedAbsent = MediaField.Tagline | MediaField.Summary,
			}
		);

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromDays(6), Now)).IsTrue();
	}

	[Test]
	public async Task IsFresh_WhenAGapIsUnconfirmed_GoesStaleAfterTheCooldown()
	{
		var stored = IdentifiedMedia.From(MediaFakes.CompletePartialMovie() with { Tagline = null, Runtime = null, ConfirmedAbsent = MediaField.Tagline });

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromHours(2), Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenNoGapIsConfirmed_GoesStaleAfterTheCooldownEvenForATagline()
	{
		var stored = IdentifiedMedia.From(MediaFakes.CompletePartialMovie() with { Tagline = null });

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromHours(2), Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenEveryGapIsConfirmedButOlderThanTheWeek_IsStale()
	{
		var stored = IdentifiedMedia.From(MediaFakes.CompletePartialMovie() with { Tagline = null, ConfirmedAbsent = MediaField.Tagline });

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromDays(8), Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenPartialIsWithinTheHydrationCooldown_IsFresh()
	{
		var stored = IdentifiedMedia.From(MediaFakes.CompletePartialMovie() with { Summary = null });

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromMinutes(30), Now)).IsTrue();
	}
}
