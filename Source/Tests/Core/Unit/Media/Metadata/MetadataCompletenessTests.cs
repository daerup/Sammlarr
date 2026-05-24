using Contracts.Media;
using Contracts.Media.Search;
using Core.Media.Metadata;

namespace Core.Tests.Unit.Media.Metadata;

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
	[Arguments(nameof(MovieDetail.Title))]
	[Arguments(nameof(MovieDetail.Tagline))]
	[Arguments(nameof(MovieDetail.Summary))]
	[Arguments(nameof(MovieDetail.PosterUrl))]
	[Arguments(nameof(MovieDetail.BackdropUrl))]
	[Arguments(nameof(MovieDetail.ReleaseDate))]
	[Arguments(nameof(MovieDetail.Genres))]
	[Arguments(nameof(MovieDetail.Runtime))]
	public async Task GetMissingFields_WhenMovieFieldIsMissing_ReturnsThatField(string missingField)
	{
		var movie = CreateCompleteMovie() with
		{
			Title = missingField == nameof(MovieDetail.Title) ? string.Empty : "Title",
			Tagline = missingField == nameof(MovieDetail.Tagline) ? string.Empty : "Tagline",
			Summary = missingField == nameof(MovieDetail.Summary) ? string.Empty : "Summary",
			PosterUrl = missingField == nameof(MovieDetail.PosterUrl) ? string.Empty : "https://example.com/poster.jpg",
			BackdropUrl = missingField == nameof(MovieDetail.BackdropUrl) ? string.Empty : "https://example.com/backdrop.jpg",
			ReleaseDate = missingField == nameof(MovieDetail.ReleaseDate) ? DateOnly.MinValue : new DateOnly(2024, 1, 1),
			Genres = missingField == nameof(MovieDetail.Genres) ? [] : ["Action"],
			Runtime = missingField == nameof(MovieDetail.Runtime) ? TimeSpan.Zero : TimeSpan.FromMinutes(120),
		};

		var missing = MetadataCompleteness.GetMissingFields(movie);

		await Assert.That(missing).IsEquivalentTo(new[] { missingField });
	}

	[Test]
	[Arguments(nameof(ShowDetail.Seasons))]
	[Arguments(nameof(ShowDetail.Episodes))]
	[Arguments(nameof(ShowDetail.RuntimePerEpisode))]
	public async Task GetMissingFields_WhenShowSpecificFieldIsMissing_ReturnsThatField(string missingField)
	{
		var show = CreateCompleteShow() with
		{
			Seasons = missingField == nameof(ShowDetail.Seasons) ? 0 : 3,
			Episodes = missingField == nameof(ShowDetail.Episodes) ? 0 : 30,
			RuntimePerEpisode = missingField == nameof(ShowDetail.RuntimePerEpisode) ? TimeSpan.Zero : TimeSpan.FromMinutes(45),
		};

		var missing = MetadataCompleteness.GetMissingFields(show);

		await Assert.That(missing).IsEquivalentTo(new[] { missingField });
	}

	[Test]
	public async Task GetMissingFields_WhenMultipleFieldsMissing_ReturnsAll()
	{
		var movie = CreateCompleteMovie() with { Title = string.Empty, Summary = string.Empty, Runtime = TimeSpan.Zero };

		var missing = MetadataCompleteness.GetMissingFields(movie);

		await Assert.That(missing).IsEquivalentTo(new[] { nameof(MovieDetail.Title), nameof(MovieDetail.Summary), nameof(MovieDetail.Runtime) });
	}

	[Test]
	public async Task GetMissingFields_WhenWhitespaceOnlyTitle_ReturnsTitle()
	{
		var movie = CreateCompleteMovie() with { Title = "   " };

		var missing = MetadataCompleteness.GetMissingFields(movie);

		await Assert.That(missing).Contains(nameof(MovieDetail.Title));
	}

	[Test]
	public async Task GetMissingFields_WhenUnknownDetailType_ReturnsUnknownMarker()
	{
		var unknown = new UnresolvedMedia
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Movie)]),
			MediaType = MediaType.Movie,
			Title = "Title",
			ReleaseDate = new DateOnly(2024, 1, 1),
		};

		var missing = MetadataCompleteness.GetMissingFields(unknown);

		await Assert.That(missing).IsEquivalentTo(new[] { "UnknownDetailType" });
	}

	private static MovieDetail CreateCompleteMovie() =>
		new()
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Movie)]),
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

	private static ShowDetail CreateCompleteShow() =>
		new()
		{
			Identity = new MediaIdentity(default, [TmdbId.From("1", MediaType.Show)]),
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
