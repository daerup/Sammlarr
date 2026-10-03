using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class RatingSummaryTests
{
	[Test]
	public async Task From_WhenSources_KeepsOneRatingPerSource()
	{
		var summary = RatingSummary.From([new TmdbRating(8f, new None()), new RottenTomatoesCriticsRating(95)]);

		await Assert.That(summary.Sources.Count).IsEqualTo(2);
		await Assert.That(summary.Of(RatingSource.RottenTomatoesCritics).Map(rating => rating.SammlarrValue)).IsSome().And.IsEqualTo(9.5f);
	}

	[Test]
	public async Task From_WhenEmpty_IsNone()
	{
		var summary = RatingSummary.From([]);

		await Assert.That(summary).IsEqualTo(RatingSummary.None);
	}

	[Test]
	public async Task From_WhenASourceRepeats_KeepsItsFirstRating()
	{
		var summary = RatingSummary.From([new TmdbRating(8f, new None()), new TmdbRating(6f, new None())]);

		await Assert.That(summary.Of(RatingSource.Tmdb)).IsSome().And.IsEqualTo(new TmdbRating(8f, new None()));
		await Assert.That(summary.Sources.Count).IsEqualTo(1);
	}

	[Test]
	public async Task Equals_WhenSameSources_AreEqual()
	{
		var first = RatingSummary.From([new TmdbRating(8f, new None())]);
		var second = RatingSummary.From([new TmdbRating(8f, new None())]);

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task FillFrom_WhenTheOtherRatesMoreSources_KeepsMineAndAddsTheirs()
	{
		var mine = RatingSummary.From([new TmdbRating(8.6f, Some.Of(7705))]);
		var theirs = RatingSummary.From([new TmdbRating(8.5f, Some.Of(10)), new ImdbRating(8.2f, Some.Of(542_195))]);

		var filled = mine.FillFrom(theirs);

		await Assert.That(filled.Of(RatingSource.Tmdb)).IsSome().And.IsEqualTo(new TmdbRating(8.6f, Some.Of(7705)));
		await Assert.That(filled.Of(RatingSource.Imdb)).IsSome().And.IsEqualTo(new ImdbRating(8.2f, Some.Of(542_195)));
	}

	[Test]
	public async Task FillFrom_WhenMineHasNoVotesAndTheirsHas_TakesTheirs()
	{
		var plexCopy = RatingSummary.From([new TmdbRating(8.9f, new None())]);
		var tmdbOwn = RatingSummary.From([new TmdbRating(9.0f, Some.Of(8))]);

		var filled = plexCopy.FillFrom(tmdbOwn);

		await Assert.That(filled.Of(RatingSource.Tmdb)).IsSome().And.IsEqualTo(new TmdbRating(9.0f, Some.Of(8)));
	}
}
