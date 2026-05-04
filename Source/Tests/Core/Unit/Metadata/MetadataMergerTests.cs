using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;

namespace Core.Tests.Unit.Metadata;

public class MetadataMergerTests
{
	[Test]
	public async Task FillMissing_WhenMoviePrimaryHasGaps_FillsFromSecondary()
	{
		var primary = new MovieDetailResult
		{
			SourceId = TmdbId.From("1", MediaType.Movie),
			AdditionalIds = [],
			Title = "Primary Title",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = "primary-poster",
			BackdropUrl = string.Empty,
			ReleaseDate = DateOnly.MinValue,
			Genres = [],
			Runtime = TimeSpan.Zero,
		};
		var secondary = new MovieDetailResult
		{
			SourceId = TmdbId.From("2", MediaType.Movie),
			AdditionalIds = [],
			Title = "Secondary Title",
			Tagline = "Secondary Tagline",
			Summary = "Secondary Summary",
			PosterUrl = "secondary-poster",
			BackdropUrl = "secondary-backdrop",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

		var result = (MovieDetailResult)MetadataMerger.FillMissing(primary, secondary);

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
		var primary = new MovieDetailResult
		{
			SourceId = TmdbId.From("1", MediaType.Movie),
			AdditionalIds = [],
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

		var result = (MovieDetailResult)MetadataMerger.FillMissing(primary, secondary);

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
		var primary = new ShowDetailResult
		{
			SourceId = TmdbId.From("1", MediaType.Show),
			AdditionalIds = [],
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
		var secondary = new ShowDetailResult
		{
			SourceId = TmdbId.From("2", MediaType.Show),
			AdditionalIds = [],
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

		var result = (ShowDetailResult)MetadataMerger.FillMissing(primary, secondary);

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
	public async Task FillMissing_WhenTypesDoNotMatch_ReturnsPrimary()
	{
		var movie = new MovieDetailResult
		{
			SourceId = TmdbId.From("1", MediaType.Movie),
			AdditionalIds = [],
			Title = "Movie",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = DateOnly.MinValue,
			Genres = [],
			Runtime = TimeSpan.Zero,
		};
		var show = new ShowDetailResult
		{
			SourceId = TmdbId.From("2", MediaType.Show),
			AdditionalIds = [],
			Title = "Show",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Drama"],
			Seasons = 2,
			Episodes = 20,
			RuntimePerEpisode = TimeSpan.FromMinutes(30),
		};

		var result = MetadataMerger.FillMissing(movie, show);

		await Assert.That(result).IsSameReferenceAs(movie);
	}

	[Test]
	public async Task FillMissing_WhenPrimaryHasWhitespaceTagline_FallsBackToSecondary()
	{
		var primary = new MovieDetailResult
		{
			SourceId = TmdbId.From("1", MediaType.Movie),
			AdditionalIds = [],
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

		var result = (MovieDetailResult)MetadataMerger.FillMissing(primary, secondary);

		await Assert.That(result.Tagline).IsEqualTo("Secondary Tagline");
	}
}
