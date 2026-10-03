using Domain.Media;
using Domain.Media.Metadata;

namespace Domain.Tests.Media.Metadata;

public class MediaFreshnessTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task IsFresh_WhenNeverFetched_IsStale()
	{
		var stored = MediaFakes.CompleteMovie();

		await Assert.That(MediaFreshness.IsFresh(stored, null, Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenCompleteAndFetchedWithinTheWeek_IsFresh()
	{
		var stored = MediaFakes.CompleteMovie();

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromDays(6), Now)).IsTrue();
	}

	[Test]
	public async Task IsFresh_WhenCompleteAndOlderThanTheWeek_IsStale()
	{
		var stored = MediaFakes.CompleteMovie();

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromDays(8), Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenEveryGapIsConfirmed_StaysFreshForTheWeek()
	{
		var stored = Stored(MediaFakes.CompleteMovieDetails() with { Tagline = null, Summary = null }, MediaField.Tagline | MediaField.Summary);

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromDays(6), Now)).IsTrue();
	}

	[Test]
	public async Task IsFresh_WhenAGapIsUnconfirmed_GoesStaleAfterTheCooldown()
	{
		var stored = Stored(MediaFakes.CompleteMovieDetails() with { Tagline = null, Runtime = null }, MediaField.Tagline);

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromHours(2), Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenNoGapIsConfirmed_GoesStaleAfterTheCooldownEvenForATagline()
	{
		var stored = Stored(MediaFakes.CompleteMovieDetails() with { Tagline = null }, MediaField.None);

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromHours(2), Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenEveryGapIsConfirmedButOlderThanTheWeek_IsStale()
	{
		var stored = Stored(MediaFakes.CompleteMovieDetails() with { Tagline = null }, MediaField.Tagline);

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromDays(8), Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenPartialIsWithinTheHydrationCooldown_IsFresh()
	{
		var stored = Stored(MediaFakes.CompleteMovieDetails() with { Summary = null }, MediaField.None);

		await Assert.That(MediaFreshness.IsFresh(stored, Now - TimeSpan.FromMinutes(30), Now)).IsTrue();
	}

	private static IdentifiedMedia Stored(MediaDetails details, MediaField confirmedAbsent)
	{
		return new(MediaFakes.MovieIdentity(), details) { ConfirmedAbsent = confirmedAbsent };
	}
}
