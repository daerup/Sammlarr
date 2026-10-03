using Bogus;
using Domain.Media;

namespace Domain.Tests;

internal static class MediaFakes
{
	public static MediaIdentity MovieIdentity()
	{
		var faker = new Faker();
		return new(MediaId.New(), [TmdbId.From(faker.Random.Int(1, 1_000_000).ToString(), MediaType.Movie)]);
	}

	public static MovieDetails CompleteMovieDetails()
	{
		var faker = new Faker();
		return new()
		{
			Title = faker.Lorem.Sentence(2),
			Tagline = faker.Lorem.Sentence(3),
			Summary = faker.Lorem.Paragraph(),
			PosterUrl = faker.Internet.Url(),
			BackdropUrl = faker.Internet.Url(),
			ReleaseDate = DateOnly.FromDateTime(faker.Date.Past()),
			Genres = [.. faker.Lorem.Words(2)],
			Rating = EverySourceRating(),
			Runtime = TimeSpan.FromMinutes(faker.Random.Int(80, 200)),
		};
	}

	public static RatingSummary EverySourceRating()
	{
		var faker = new Faker();
		return RatingSummary.From([
			new TmdbRating(faker.Random.Float(1, 10), Some.Of(faker.Random.Int(1, 50_000))),
			new ImdbRating(faker.Random.Float(1, 10), Some.Of(faker.Random.Int(1, 2_000_000))),
			new RottenTomatoesCriticsRating(faker.Random.Int(0, 100)),
			new RottenTomatoesAudienceRating(faker.Random.Int(0, 100)),
		]);
	}

	public static MatchedMedia CompleteMovie() => new(MovieIdentity(), CompleteMovieDetails());
}
