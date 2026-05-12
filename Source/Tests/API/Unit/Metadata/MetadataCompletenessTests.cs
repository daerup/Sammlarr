using API.Metadata;
using Contracts.Media;
using Contracts.Media.Search;

namespace API.Tests.Unit.Metadata;

public class MetadataCompletenessTests
{
	[Test]
	public async Task GetMissingFields_WhenMovieHasAllFieldsPopulated_ReturnsEmpty()
	{
		var movie = CreateCompleteMovie();

		var missing = MetadataCompleteness.GetMissingFields(movie);

		await Assert.That(missing).IsEmpty();
	}

	[Test]
	public async Task GetMissingFields_WhenShowHasAllFieldsPopulated_ReturnsEmpty()
	{
		var show = CreateCompleteShow();

		var missing = MetadataCompleteness.GetMissingFields(show);

		await Assert.That(missing).IsEmpty();
	}

	[Test]
	[Arguments(nameof(MovieDetailResult.Title))]
	[Arguments(nameof(MovieDetailResult.Tagline))]
	[Arguments(nameof(MovieDetailResult.Summary))]
	[Arguments(nameof(MovieDetailResult.PosterUrl))]
	[Arguments(nameof(MovieDetailResult.BackdropUrl))]
	[Arguments(nameof(MovieDetailResult.ReleaseDate))]
	[Arguments(nameof(MovieDetailResult.Genres))]
	[Arguments(nameof(MovieDetailResult.Runtime))]
	public async Task GetMissingFields_WhenMovieFieldIsMissing_ReturnsThatField(string missingField)
	{
		var movie = CreateCompleteMovie() with
		{
			Title = missingField == nameof(MovieDetailResult.Title) ? string.Empty : "Title",
			Tagline = missingField == nameof(MovieDetailResult.Tagline) ? string.Empty : "Tagline",
			Summary = missingField == nameof(MovieDetailResult.Summary) ? string.Empty : "Summary",
			PosterUrl = missingField == nameof(MovieDetailResult.PosterUrl) ? string.Empty : "https://example.com/poster.jpg",
			BackdropUrl = missingField == nameof(MovieDetailResult.BackdropUrl) ? string.Empty : "https://example.com/backdrop.jpg",
			ReleaseDate = missingField == nameof(MovieDetailResult.ReleaseDate) ? DateOnly.MinValue : new DateOnly(2024, 1, 1),
			Genres = missingField == nameof(MovieDetailResult.Genres) ? [] : ["Action"],
			Runtime = missingField == nameof(MovieDetailResult.Runtime) ? TimeSpan.Zero : TimeSpan.FromMinutes(120),
		};

		var missing = MetadataCompleteness.GetMissingFields(movie);

		await Assert.That(missing).IsEquivalentTo(new[] { missingField });
	}

	[Test]
	[Arguments(nameof(ShowDetailResult.Seasons))]
	[Arguments(nameof(ShowDetailResult.Episodes))]
	[Arguments(nameof(ShowDetailResult.RuntimePerEpisode))]
	public async Task GetMissingFields_WhenShowSpecificFieldIsMissing_ReturnsThatField(string missingField)
	{
		var show = CreateCompleteShow() with
		{
			Seasons = missingField == nameof(ShowDetailResult.Seasons) ? 0 : 3,
			Episodes = missingField == nameof(ShowDetailResult.Episodes) ? 0 : 30,
			RuntimePerEpisode = missingField == nameof(ShowDetailResult.RuntimePerEpisode) ? TimeSpan.Zero : TimeSpan.FromMinutes(45),
		};

		var missing = MetadataCompleteness.GetMissingFields(show);

		await Assert.That(missing).IsEquivalentTo(new[] { missingField });
	}

	[Test]
	public async Task GetMissingFields_WhenMultipleFieldsMissing_ReturnsAll()
	{
		var movie = CreateCompleteMovie() with
		{
			Title = string.Empty,
			Summary = string.Empty,
			Runtime = TimeSpan.Zero,
		};

		var missing = MetadataCompleteness.GetMissingFields(movie);

		await Assert.That(missing).IsEquivalentTo(new[] { nameof(MovieDetailResult.Title), nameof(MovieDetailResult.Summary), nameof(MovieDetailResult.Runtime) });
	}

	[Test]
	public async Task GetMissingFields_WhenWhitespaceOnlyTitle_ReturnsTitle()
	{
		var movie = CreateCompleteMovie() with { Title = "   " };

		var missing = MetadataCompleteness.GetMissingFields(movie);

		await Assert.That(missing).Contains(nameof(MovieDetailResult.Title));
	}

	[Test]
	public async Task GetMissingFields_WhenUnknownDetailType_ReturnsUnknownMarker()
	{
		var unknown = new UnresolvedMediaResult
		{
			SourceId = TmdbId.From("1", MediaType.Movie),
			AdditionalIds = [],
			MediaType = MediaType.Movie,
			Title = "Title",
			ReleaseDate = new DateOnly(2024, 1, 1),
		};

		var missing = MetadataCompleteness.GetMissingFields(unknown);

		await Assert.That(missing).IsEquivalentTo(new[] { "UnknownDetailType" });
	}

	private static MovieDetailResult CreateCompleteMovie() =>
		new()
		{
			SourceId = TmdbId.From("1", MediaType.Movie),
			AdditionalIds = [],
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

	private static ShowDetailResult CreateCompleteShow() =>
		new()
		{
			SourceId = TmdbId.From("1", MediaType.Show),
			AdditionalIds = [],
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Drama"],
			Seasons = 3,
			Episodes = 30,
			RuntimePerEpisode = TimeSpan.FromMinutes(45),
		};
}
