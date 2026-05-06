using API.Metadata;
using Contracts.Media;
using Contracts.Media.Search;

namespace API.Tests.Unit.Metadata;

public class MetadataCompletenessTests
{
	[Test]
	public async Task IsComplete_WhenMovieHasAllFieldsPopulated_ReturnsTrue()
	{
		var movie = CreateCompleteMovie();

		var result = MetadataCompleteness.IsComplete(movie);

		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task IsComplete_WhenShowHasAllFieldsPopulated_ReturnsTrue()
	{
		var show = CreateCompleteShow();

		var result = MetadataCompleteness.IsComplete(show);

		await Assert.That(result).IsTrue();
	}

	[Test]
	[Arguments("title")]
	[Arguments("tagline")]
	[Arguments("summary")]
	[Arguments("poster")]
	[Arguments("backdrop")]
	[Arguments("releaseDate")]
	[Arguments("genres")]
	[Arguments("runtime")]
	public async Task IsComplete_WhenMovieFieldIsMissing_ReturnsFalse(string missingField)
	{
		var movie = CreateCompleteMovie() with
		{
			Title = missingField == "title" ? string.Empty : "Title",
			Tagline = missingField == "tagline" ? string.Empty : "Tagline",
			Summary = missingField == "summary" ? string.Empty : "Summary",
			PosterUrl = missingField == "poster" ? string.Empty : "https://example.com/poster.jpg",
			BackdropUrl = missingField == "backdrop" ? string.Empty : "https://example.com/backdrop.jpg",
			ReleaseDate = missingField == "releaseDate" ? DateOnly.MinValue : new DateOnly(2024, 1, 1),
			Genres = missingField == "genres" ? [] : ["Action"],
			Runtime = missingField == "runtime" ? TimeSpan.Zero : TimeSpan.FromMinutes(120),
		};

		var result = MetadataCompleteness.IsComplete(movie);

		await Assert.That(result).IsFalse();
	}

	[Test]
	[Arguments("seasons")]
	[Arguments("episodes")]
	[Arguments("runtimePerEpisode")]
	public async Task IsComplete_WhenShowSpecificFieldIsMissing_ReturnsFalse(string missingField)
	{
		var show = CreateCompleteShow() with
		{
			Seasons = missingField == "seasons" ? 0 : 3,
			Episodes = missingField == "episodes" ? 0 : 30,
			RuntimePerEpisode = missingField == "runtimePerEpisode" ? TimeSpan.Zero : TimeSpan.FromMinutes(45),
		};

		var result = MetadataCompleteness.IsComplete(show);

		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task IsComplete_WhenWhitespaceOnlyTitle_ReturnsFalse()
	{
		var movie = CreateCompleteMovie() with { Title = "   " };

		var result = MetadataCompleteness.IsComplete(movie);

		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task IsComplete_WhenUnknownDetailType_ReturnsFalse()
	{
		var unknown = new UnresolvedMediaResult
		{
			SourceId = TmdbId.From("1", MediaType.Movie),
			AdditionalIds = [],
			MediaType = MediaType.Movie,
			Title = "Title",
			ReleaseDate = new DateOnly(2024, 1, 1),
		};

		var result = MetadataCompleteness.IsComplete(unknown);

		await Assert.That(result).IsFalse();
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
