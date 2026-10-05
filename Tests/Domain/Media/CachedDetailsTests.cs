using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class CachedDetailsTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset LastRefresh = Now.AddDays(-14);
	private static readonly ProviderAnswer TmdbFilledEverything = new(MediaProvider.Tmdb, MediaFields.None);
	private static readonly ProviderAnswer PlexLackedTheAudience = new(MediaProvider.Plex, MediaFields.Rated([RatingSource.RottenTomatoesAudience]));

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
		await Assert.That(Cached(Stored, Now, TmdbFilledEverything, PlexLackedTheAudience).Answered).IsEquivalentTo([MediaProvider.Tmdb, MediaProvider.Plex]);
	}

	[Test]
	public async Task Replacing_WhenEveryProviderThatAnsweredBeforeAnsweredAgain_DropsWhatTheyNoLongerHave()
	{
		var refreshed = Cached(Stored with { Tagline = null }, Now, TmdbFilledEverything, PlexLackedTheAudience);

		var replaced = refreshed.Replacing(Cached(Stored, LastRefresh, TmdbFilledEverything, PlexLackedTheAudience));

		await Assert.That(replaced).IsEqualTo(refreshed);
	}

	[Test]
	public async Task Replacing_WhenAProviderThatAnsweredBeforeDidNotAnswer_KeepsWhatTheRefreshLacks()
	{
		var refreshed = Cached(
			new MovieDetails { Title = "Dune", Ratings = MediaRatings.From([new TmdbRating(7.9f, Some.Of(12_500))]) },
			Now,
			TmdbFilledEverything
		);

		var laid = refreshed.Replacing(Cached(Stored, LastRefresh, TmdbFilledEverything, PlexLackedTheAudience));

		await Assert.That(laid.Details.Tagline).IsEqualTo("Fear is the mind-killer.");
		await Assert.That(laid.Details.Ratings.Of(RatingSource.Tmdb).Map(rating => rating.SammlarrValue)).IsSome().And.IsEqualTo(7.9f);
		await Assert
			.That(laid.Details.Ratings.All.Select(rating => rating.Source))
			.IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb, RatingSource.RottenTomatoesCritics]);
		await Assert.That(laid.RefreshedAt).IsEqualTo(Now);
	}

	[Test]
	public async Task Replacing_WhenTheStoredCopyRecordsNoAnswers_KeepsWhatTheRefreshLacks()
	{
		var refreshed = Cached(Stored with { Tagline = null }, Now, TmdbFilledEverything, PlexLackedTheAudience);

		var laid = refreshed.Replacing(Cached(Stored, LastRefresh));

		await Assert.That(laid.Details.Tagline).IsEqualTo("Fear is the mind-killer.");
	}

	[Test]
	public async Task Replacing_WhenLaidOver_KeepsAStoredRatingWithVotesOverANewOneWithout()
	{
		var refreshed = Cached(new MovieDetails { Title = "Dune", Ratings = MediaRatings.From([new TmdbRating(7.9f, new None())]) }, Now, PlexLackedTheAudience);

		var laid = refreshed.Replacing(Cached(Stored, LastRefresh, TmdbFilledEverything, PlexLackedTheAudience));

		await Assert.That(laid.Details.Ratings.Of(RatingSource.Tmdb).Map(rating => rating.Votes)).IsSome().And.IsEqualTo(12_000);
	}

	[Test]
	public async Task Replacing_WhenLaidOver_TakesTheNewAnswersAndKeepsTheAbsentProvidersOldOne()
	{
		var tmdbLackedTheTagline = new ProviderAnswer(MediaProvider.Tmdb, MediaField.Tagline);
		var refreshed = Cached(Stored with { Tagline = null }, Now, tmdbLackedTheTagline);

		var laid = refreshed.Replacing(Cached(Stored, LastRefresh, TmdbFilledEverything, PlexLackedTheAudience));

		await Assert.That(laid.Answers).IsEquivalentTo([tmdbLackedTheTagline, PlexLackedTheAudience]);
	}

	private static CachedDetails Cached(MediaDetails details, DateTimeOffset refreshedAt, params ProviderAnswer[] answers) =>
		new(details, refreshedAt, refreshedAt, [.. answers]);
}
