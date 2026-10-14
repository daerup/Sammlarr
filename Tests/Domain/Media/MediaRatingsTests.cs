using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class MediaRatingsTests
{
	[Test]
	public async Task From_WhenSources_KeepsOneRatingPerSource()
	{
		var ratings = MediaRatings.From([new TmdbRating(8f, new None()), new RottenTomatoesCriticsRating(95)]);

		await Assert.That(ratings.All.Count).IsEqualTo(2);
		await Assert.That(ratings.Of(RatingSource.RottenTomatoesCritics).Map(rating => rating.SammlarrValue)).IsSome().And.IsEqualTo(9.5f);
	}

	[Test]
	public async Task From_WhenEmpty_IsNone()
	{
		var ratings = MediaRatings.From([]);

		await Assert.That(ratings).IsEqualTo(MediaRatings.None);
	}

	[Test]
	public async Task From_WhenASourceRepeats_KeepsItsFirstRating()
	{
		var ratings = MediaRatings.From([new TmdbRating(8f, new None()), new TmdbRating(6f, new None())]);

		await Assert.That(ratings.Of(RatingSource.Tmdb)).IsSome().And.IsEqualTo(new TmdbRating(8f, new None()));
		await Assert.That(ratings.All.Count).IsEqualTo(1);
	}

	[Test]
	public async Task FillFrom_WhenTheOtherRatesMoreSources_KeepsMineAndAddsTheirs()
	{
		var mine = MediaRatings.From([new TmdbRating(8.6f, Some.Of(7705))]);
		var theirs = MediaRatings.From([new TmdbRating(8.5f, Some.Of(10)), new ImdbRating(8.2f, Some.Of(542_195))]);

		var filled = mine.FillFrom(theirs);

		await Assert.That(filled.Of(RatingSource.Tmdb)).IsSome().And.IsEqualTo(new TmdbRating(8.6f, Some.Of(7705)));
		await Assert.That(filled.Of(RatingSource.Imdb)).IsSome().And.IsEqualTo(new ImdbRating(8.2f, Some.Of(542_195)));
	}

	[Test]
	public async Task FillFrom_WhenMineHasNoVotesAndTheirsHas_TakesTheirs()
	{
		var plexCopy = MediaRatings.From([new TmdbRating(8.9f, new None())]);
		var tmdbOwn = MediaRatings.From([new TmdbRating(9.0f, Some.Of(8))]);

		var filled = plexCopy.FillFrom(tmdbOwn);

		await Assert.That(filled.Of(RatingSource.Tmdb)).IsSome().And.IsEqualTo(new TmdbRating(9.0f, Some.Of(8)));
	}
}
