using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Core.Media.Metadata;

namespace Core.Tests.Unit.Media.Metadata;

public class MetadataMergerTests
{
	[Test]
	public async Task FillMissing_WhenMoviePrimaryHasGaps_FillsFromSecondary()
	{
		var primary = new MovieDetail
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Movie)]),
			Title = "Primary Title",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = "primary-poster",
			BackdropUrl = string.Empty,
			ReleaseDate = DateOnly.MinValue,
			Genres = [],
			Runtime = TimeSpan.Zero,
		};
		var secondary = new MovieDetail
		{
			Identity = new MediaIdentity(default, [TmdbId.From("2", MediaType.Movie)]),
			Title = "Secondary Title",
			Tagline = "Secondary Tagline",
			Summary = "Secondary Summary",
			PosterUrl = "secondary-poster",
			BackdropUrl = "secondary-backdrop",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

		var result = (MovieDetail)MetadataMerger.FillMissing(primary, secondary);

		using var _ = Assert.Multiple();
		await Assert.That(result.Title).IsEqualTo("Primary Title");
		await Assert.That(result.Tagline).IsEqualTo("Secondary Tagline");
		await Assert.That(result.Summary).IsEqualTo("Secondary Summary");
		await Assert.That(result.PosterUrl).IsEqualTo("primary-poster");
		await Assert.That(result.BackdropUrl).IsEqualTo("secondary-backdrop");
		await Assert.That(result.ReleaseDate).IsEqualTo(new DateOnly(2024, 1, 1));
		await Assert.That(result.Genres).IsEquivalentTo(new[] { "Action" });
		await Assert.That(result.Runtime).IsEqualTo(TimeSpan.FromMinutes(120));
	}

	[Test]
	public async Task FillMissing_WhenMoviePrimaryHasAllValues_KeepsAllPrimaryValues()
	{
		var primary = new MovieDetail
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Movie)]),
			Title = "Primary Title",
			Tagline = "Primary Tagline",
			Summary = "Primary Summary",
			PosterUrl = "primary-poster",
			BackdropUrl = "primary-backdrop",
			ReleaseDate = new DateOnly(2023, 5, 1),
			Genres = ["Comedy"],
			Runtime = TimeSpan.FromMinutes(95),
		};
		var secondary = primary with
		{
			Title = "Secondary",
			Tagline = "Secondary",
			Summary = "Secondary",
			PosterUrl = "secondary-poster",
			BackdropUrl = "secondary-backdrop",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(180),
		};

		var result = (MovieDetail)MetadataMerger.FillMissing(primary, secondary);

		using var _ = Assert.Multiple();
		await Assert.That(result.Title).IsEqualTo("Primary Title");
		await Assert.That(result.Tagline).IsEqualTo("Primary Tagline");
		await Assert.That(result.Summary).IsEqualTo("Primary Summary");
		await Assert.That(result.PosterUrl).IsEqualTo("primary-poster");
		await Assert.That(result.BackdropUrl).IsEqualTo("primary-backdrop");
		await Assert.That(result.ReleaseDate).IsEqualTo(new DateOnly(2023, 5, 1));
		await Assert.That(result.Genres).IsEquivalentTo(new[] { "Comedy" });
		await Assert.That(result.Runtime).IsEqualTo(TimeSpan.FromMinutes(95));
	}

	[Test]
	public async Task FillMissing_WhenShowPrimaryHasGaps_FillsFromSecondary()
	{
		var primary = new ShowDetail
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Show)]),
			Title = "Primary Title",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = DateOnly.MinValue,
			Genres = [],
			Seasons = 0,
			Episodes = 0,
			RuntimePerEpisode = TimeSpan.Zero,
		};
		var secondary = new ShowDetail
		{
			Identity = new MediaIdentity(default, [TmdbId.From("2", MediaType.Show)]),
			Title = "Secondary Title",
			Tagline = "Secondary Tagline",
			Summary = "Secondary Summary",
			PosterUrl = "secondary-poster",
			BackdropUrl = "secondary-backdrop",
			ReleaseDate = new DateOnly(2022, 2, 2),
			Genres = ["Drama"],
			Seasons = 4,
			Episodes = 40,
			RuntimePerEpisode = TimeSpan.FromMinutes(45),
		};

		var result = (ShowDetail)MetadataMerger.FillMissing(primary, secondary);

		using var _ = Assert.Multiple();
		await Assert.That(result.Title).IsEqualTo("Primary Title");
		await Assert.That(result.Tagline).IsEqualTo("Secondary Tagline");
		await Assert.That(result.Summary).IsEqualTo("Secondary Summary");
		await Assert.That(result.PosterUrl).IsEqualTo("secondary-poster");
		await Assert.That(result.Seasons).IsEqualTo(4);
		await Assert.That(result.Episodes).IsEqualTo(40);
		await Assert.That(result.RuntimePerEpisode).IsEqualTo(TimeSpan.FromMinutes(45));
	}

	[Test]
	public async Task FillMissing_WhenPrimaryHasWhitespaceTagline_FallsBackToSecondary()
	{
		var primary = new MovieDetail
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Movie)]),
			Title = "Title",
			Tagline = "   ",
			Summary = "Summary",
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Comedy"],
			Runtime = TimeSpan.FromMinutes(90),
		};
		var secondary = primary with { Tagline = "Secondary Tagline" };

		var result = (MovieDetail)MetadataMerger.FillMissing(primary, secondary);

		await Assert.That(result.Tagline).IsEqualTo("Secondary Tagline");
	}

	[Test]
	public async Task WithRatings_WhenDetailsHasNoRatings_ReplacesWithProvidedRatings()
	{
		var details = MovieWithRatings([]);
		var ratings = new[] { new MediaRating(RatingSource.Tmdb, 7.5f, 1234) };

		var result = (MovieDetail)MetadataMerger.WithRatings(details, ratings);

		await Assert.That(result.Ratings).IsEquivalentTo(ratings);
	}

	[Test]
	public async Task WithRatings_WhenSameSourcePresent_KeepsExistingRating()
	{
		var existing = new MediaRating(RatingSource.Tmdb, 8.0f, 5000);
		var details = MovieWithRatings([existing]);

		var result = (MovieDetail)MetadataMerger.WithRatings(details, [new MediaRating(RatingSource.Tmdb, 6.0f, 10)]);

		await Assert.That(result.Ratings).IsEquivalentTo(new[] { existing });
	}

	[Test]
	public async Task FillMissing_WhenSecondaryAddsNewRatingSource_MergesIntoMovie()
	{
		var primary = MovieWithRatings([new MediaRating(RatingSource.Tmdb, 7.0f, 100)]);
		var secondary = MovieWithRatings([new MediaRating(RatingSource.Tmdb, 9.9f, 999_999)]);

		var result = (MovieDetail)MetadataMerger.FillMissing(primary, secondary);

		await Assert.That(result.Ratings.Count).IsEqualTo(1);
		await Assert.That(result.Ratings[0].NormalizedValue).IsEqualTo(7.0f);
	}

	[Test]
	public async Task FillMissing_WhenPrimaryHasNoRatings_TakesSecondaryRatingsForShow()
	{
		var primary = ShowWithRatings([]);
		var secondary = ShowWithRatings([new MediaRating(RatingSource.Tmdb, 8.4f, 42)]);

		var result = (ShowDetail)MetadataMerger.FillMissing(primary, secondary);

		await Assert.That(result.Ratings.Count).IsEqualTo(1);
		await Assert.That(result.Ratings[0].Source).IsEqualTo(RatingSource.Tmdb);
		await Assert.That(result.Ratings[0].NormalizedValue).IsEqualTo(8.4f);
	}

	[Test]
	public async Task FillMissing_UnionsExternals_KeepingPrimaryOrder()
	{
		var primaryTmdb = TmdbId.From("1", MediaType.Movie);
		var sharedPlex = PlexId.From("plex-1", MediaType.Movie);
		var secondaryImdb = ImdbId.From("tt0001", MediaType.Movie);
		var primary = MovieWithExternals([primaryTmdb, sharedPlex]);
		var secondary = MovieWithExternals([sharedPlex, secondaryImdb]);

		var result = (MovieDetail)MetadataMerger.FillMissing(primary, secondary);

		await Assert.That(result.Identity.Externals.Count).IsEqualTo(3);
		await Assert.That(result.Identity.Externals[0]).IsEqualTo<IExternalMediaId>(primaryTmdb);
		await Assert.That(result.Identity.Externals[1]).IsEqualTo<IExternalMediaId>(sharedPlex);
		await Assert.That(result.Identity.Externals[2]).IsEqualTo<IExternalMediaId>(secondaryImdb);
	}

	[Test]
	public async Task FillMissing_WhenSecondaryAddsNoExternals_ReturnsPrimaryReference()
	{
		var primary = MovieWithExternals([TmdbId.From("1", MediaType.Movie)]);
		var secondary = MovieWithExternals([]);

		var result = (MovieDetail)MetadataMerger.FillMissing(primary, secondary);

		await Assert.That(result.Identity.Externals).IsSameReferenceAs(primary.Identity.Externals);
	}

	private static MovieDetail MovieWithExternals(IReadOnlyList<IExternalMediaId> externals) =>
		new()
		{
			Identity = new MediaIdentity(default, externals),
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
			Ratings = [],
		};

	private static MovieDetail MovieWithRatings(IReadOnlyList<MediaRating> ratings) =>
		new()
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Movie)]),
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
			Ratings = ratings,
		};

	private static ShowDetail ShowWithRatings(IReadOnlyList<MediaRating> ratings) =>
		new()
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Show)]),
			Title = "Show",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Drama"],
			Seasons = 1,
			Episodes = 10,
			RuntimePerEpisode = TimeSpan.FromMinutes(45),
			Ratings = ratings,
		};
}
