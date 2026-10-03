using Bogus;
using Domain.Media;
using Domain.Providers;

namespace Domain.Tests;

internal static class MediaFakes
{
	public static MediaIdentity MovieIdentity()
	{
		var faker = new Faker();
		return new(MediaId.From(Guid.NewGuid()), [TmdbId.From(faker.Random.Int(1, 1_000_000).ToString(), MediaType.Movie)]);
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
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, faker.Random.Float(1, 10))]),
			Runtime = TimeSpan.FromMinutes(faker.Random.Int(80, 200)),
		};
	}

	public static IdentifiedMedia CompleteMovie() => new(MovieIdentity(), CompleteMovieDetails());
}
