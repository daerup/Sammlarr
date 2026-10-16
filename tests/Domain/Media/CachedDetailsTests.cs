using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class CachedDetailsTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan FreshFor = TimeSpan.FromDays(14);
	private static readonly ProviderAnswer TmdbFilledEverything = new(RemoteService.Tmdb, MediaFields.None, Now);
	private static readonly ProviderAnswer PlexLackedTheAudience = new(RemoteService.Plex, MediaFields.Rated([RatingSource.RottenTomatoesAudience]), Now);

	private static readonly MovieDetails Stored = new()
	{
		Title = "Dune",
		Tagline = "Fear is the mind-killer.",
		Ratings = MediaRatings.From([new TmdbRating(7.8f, Some.Of(12_000)), new ImdbRating(8.0f, Some.Of(900_000)), new RottenTomatoesCriticsRating(83)]),
	};

	[Test]
	public async Task IsStale_JustUnderTwoWeeksAfterTheRefresh_IsFalse()
	{
		await Assert.That(Cached(Stored, Now.AddDays(-14).AddMinutes(1)).IsStale(Now, FreshFor)).IsFalse();
	}

	[Test]
	public async Task IsStale_TwoWeeksAfterTheRefresh_IsTrue()
	{
		await Assert.That(Cached(Stored, Now.AddDays(-14)).IsStale(Now, FreshFor)).IsTrue();
	}

	[Test]
	public async Task IsStale_AfterARecentGapFill_StillCountsFromTheRefresh()
	{
		var filledAMinuteAgo = Cached(Stored, Now.AddDays(-15)) with { AskedAt = Now.AddMinutes(-1) };

		await Assert.That(filledAMinuteAgo.IsStale(Now, FreshFor)).IsTrue();
	}

	[Test]
	public async Task Resting_WithinTwoWeeksOfTheAnswers_NamesEveryProviderThatAnswered()
	{
		var cached = Cached(Stored, Now, TmdbFilledEverything, PlexLackedTheAudience);

		await Assert.That(cached.Resting(Now.AddDays(14).AddMinutes(-1), FreshFor)).IsEquivalentTo([RemoteService.Tmdb, RemoteService.Plex]);
	}

	[Test]
	public async Task Resting_TwoWeeksAfterTheAnswers_NamesNobody()
	{
		await Assert.That(Cached(Stored, Now, TmdbFilledEverything, PlexLackedTheAudience).Resting(Now.AddDays(14), FreshFor)).IsEmpty();
	}

	[Test]
	public async Task Resting_ForAFailure_LastsOnlyItsBackoff()
	{
		var cached = Cached(Stored, Now, TmdbFilledEverything, ProviderAnswer.Failed(RemoteService.Plex, new None(), Now));

		await Assert.That(cached.Resting(Now.AddMinutes(59), FreshFor)).IsEquivalentTo([RemoteService.Tmdb, RemoteService.Plex]);
		await Assert.That(cached.Resting(Now.AddHours(1), FreshFor)).IsEquivalentTo([RemoteService.Tmdb]);
	}

	private static CachedDetails Cached(MediaDetails details, DateTimeOffset refreshedAt, params ProviderAnswer[] answers) =>
		new(details, refreshedAt, refreshedAt, [.. answers]);
}
