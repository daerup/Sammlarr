using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class CachedDetailsTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
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
		await Assert.That(Cached(Stored, Now.AddDays(-14).AddMinutes(1)).IsStale(Now)).IsFalse();
	}

	[Test]
	public async Task IsStale_TwoWeeksAfterTheRefresh_IsTrue()
	{
		await Assert.That(Cached(Stored, Now.AddDays(-14)).IsStale(Now)).IsTrue();
	}

	[Test]
	public async Task IsStale_AfterARecentGapFill_StillCountsFromTheRefresh()
	{
		var filledAMinuteAgo = Cached(Stored, Now.AddDays(-15)) with { AskedAt = Now.AddMinutes(-1) };

		await Assert.That(filledAMinuteAgo.IsStale(Now)).IsTrue();
	}

	[Test]
	public async Task Answered_NamesEveryProviderThatAnswered()
	{
		await Assert.That(Cached(Stored, Now, TmdbFilledEverything, PlexLackedTheAudience).Answered).IsEquivalentTo([RemoteService.Tmdb, RemoteService.Plex]);
	}

	private static CachedDetails Cached(MediaDetails details, DateTimeOffset refreshedAt, params ProviderAnswer[] answers) =>
		new(details, refreshedAt, refreshedAt, [.. answers]);
}
