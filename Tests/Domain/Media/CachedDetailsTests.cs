using Domain.Media;

namespace Domain.Tests.Media;

public class CachedDetailsTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task IsFresh_WhenCompleteAndFetchedWithinTheWeek_IsFresh()
	{
		var cached = Cached(MediaFakes.CompleteMovieDetails(), TimeSpan.FromDays(6), MediaField.None);

		await Assert.That(cached.IsFresh(Now)).IsTrue();
	}

	[Test]
	public async Task IsFresh_WhenCompleteAndOlderThanTheWeek_IsStale()
	{
		var cached = Cached(MediaFakes.CompleteMovieDetails(), TimeSpan.FromDays(8), MediaField.None);

		await Assert.That(cached.IsFresh(Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenEveryGapIsConfirmed_StaysFreshForTheWeek()
	{
		var cached = Cached(
			MediaFakes.CompleteMovieDetails() with
			{
				Tagline = null,
				Summary = null,
			},
			TimeSpan.FromDays(6),
			MediaField.Tagline | MediaField.Summary
		);

		await Assert.That(cached.IsFresh(Now)).IsTrue();
	}

	[Test]
	public async Task IsFresh_WhenAGapIsUnconfirmed_GoesStaleAfterTheCooldown()
	{
		var cached = Cached(MediaFakes.CompleteMovieDetails() with { Tagline = null, Runtime = null }, TimeSpan.FromHours(2), MediaField.Tagline);

		await Assert.That(cached.IsFresh(Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenNoGapIsConfirmed_GoesStaleAfterTheCooldownEvenForATagline()
	{
		var cached = Cached(MediaFakes.CompleteMovieDetails() with { Tagline = null }, TimeSpan.FromHours(2), MediaField.None);

		await Assert.That(cached.IsFresh(Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenEveryGapIsConfirmedButOlderThanTheWeek_IsStale()
	{
		var cached = Cached(MediaFakes.CompleteMovieDetails() with { Tagline = null }, TimeSpan.FromDays(8), MediaField.Tagline);

		await Assert.That(cached.IsFresh(Now)).IsFalse();
	}

	[Test]
	public async Task IsFresh_WhenAGapIsWithinTheRetryCooldown_IsFresh()
	{
		var cached = Cached(MediaFakes.CompleteMovieDetails() with { Summary = null }, TimeSpan.FromMinutes(30), MediaField.None);

		await Assert.That(cached.IsFresh(Now)).IsTrue();
	}

	private static CachedDetails Cached(MediaDetails details, TimeSpan age, MediaField confirmedAbsent) => new(details, Now - age, confirmedAbsent);
}
