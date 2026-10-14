using Domain.Media;

namespace Domain.Tests.Media;

public class MediaMergerTests
{
	[Test]
	public async Task FillFrom_WhenEveryGapIsFilled_LeavesNothingMissing()
	{
		var first = new MovieDetails
		{
			Title = "Dune",
			Tagline = "Fear is the mind-killer",
			Summary = "Paul Atreides.",
			Genres = ["Science Fiction"],
		};
		var second = new MovieDetails
		{
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2021, 10, 22),
			Ratings = MediaFakes.EverySourceRating(),
			Runtime = TimeSpan.FromMinutes(155),
		};

		var merged = first.FillFrom(second);

		await Assert.That(merged.Missing).IsEqualTo(MediaField.None);
	}

	[Test]
	public async Task FillFrom_WhenBothSidesLinkTheSameSite_KeepsMineAndAddsTheOtherSites()
	{
		var first = new MovieDetails { PageLinks = [RottenTomatoesId.From("m/dune_2021", MediaType.Movie)] };
		var second = new MovieDetails { PageLinks = [RottenTomatoesId.From("m/dune", MediaType.Movie), MetacriticId.From("dune", MediaType.Movie)] };

		var merged = first.FillFrom(second);

		await Assert.That(merged.PageLinks.Select(link => $"{link}")).IsEquivalentTo(["RottenTomatoes:Movie:m/dune_2021", "Metacritic:Movie:dune"]);
	}

	[Test]
	public async Task Only_ForNoFields_KeepsThePageLinks()
	{
		var details = new MovieDetails { Title = "Dune", PageLinks = [MetacriticId.From("dune", MediaType.Movie)] };

		var kept = details.Only(MediaFields.None);

		await Assert.That(kept.PageLinks.Select(link => $"{link}")).IsEquivalentTo(["Metacritic:Movie:dune"]);
	}

	[Test]
	public async Task FillFrom_WhenShowsMerge_FillsTheShowFields()
	{
		var first = new ShowDetails { Title = "Breaking Bad", Seasons = 5 };
		var second = new ShowDetails
		{
			Seasons = 4,
			Episodes = 62,
			RuntimePerEpisode = TimeSpan.FromMinutes(47),
		};

		var merged = first.FillFrom(second);

		await Assert
			.That(merged)
			.IsEqualTo(
				new ShowDetails
				{
					Title = "Breaking Bad",
					Seasons = 5,
					Episodes = 62,
					RuntimePerEpisode = TimeSpan.FromMinutes(47),
				}
			);
	}

	[Test]
	public async Task Without_ForAShow_DropsTheNamedDetailsAndKeepsEveryOtherOneAndEveryRating()
	{
		var show = new ShowDetails
		{
			Title = "Shingeki no Kyojin",
			Summary = "Humans fight titans.",
			PosterUrl = "poster",
			Seasons = 4,
			Ratings = MediaRatings.From([new ImdbRating(9.1f, Some.Of(500_000))]),
		};

		var kept = show.Without(MediaField.Title | MediaField.Poster);

		await Assert.That(kept).IsEqualTo(show with { Title = null, PosterUrl = null });
	}

	[Test]
	public async Task Only_ForAShow_KeepsTheNamedDetailsAndRatingSources()
	{
		var show = new ShowDetails
		{
			Title = "Breaking Bad",
			Tagline = "Say my name.",
			Seasons = 5,
			Episodes = 62,
			RuntimePerEpisode = TimeSpan.FromMinutes(47),
			Ratings = MediaRatings.From([new TmdbRating(8.9f, Some.Of(15_000)), new ImdbRating(9.5f, Some.Of(2_300_000))]),
		};

		var kept = show.Only(MediaField.Title | MediaField.Episodes | MediaField.RuntimePerEpisode | MediaFields.Rated([RatingSource.Imdb]));

		await Assert
			.That(kept)
			.IsEqualTo(
				new ShowDetails
				{
					Title = "Breaking Bad",
					Episodes = 62,
					RuntimePerEpisode = TimeSpan.FromMinutes(47),
					Ratings = MediaRatings.From([new ImdbRating(9.5f, Some.Of(2_300_000))]),
				}
			);
	}
}
