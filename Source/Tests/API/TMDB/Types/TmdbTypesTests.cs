using API.TMDB.Types;
using Contracts.Media;

namespace API.Tests.TMDB.Types;

public class TmdbTypesTests
{
	[Test]
	public async Task TrendingResult_DisplayTitle_UsesTitleThenName()
	{
		var withTitle = CreateTrendingResult(title: "Title", name: "Name");
		var withName = CreateTrendingResult(title: null, name: "Name");

		await Assert.That(withTitle.DisplayTitle).IsEqualTo("Title");
		await Assert.That(withName.DisplayTitle).IsEqualTo("Name");
	}

	[Test]
	public async Task TrendingResult_DisplayTitle_WhenMissingValues_ThrowsInvalidOperationException()
	{
		var testee = CreateTrendingResult(title: null, name: null);

		await Assert.That(() => testee.DisplayTitle).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task TrendingResult_DisplayOriginalTitle_UsesOriginalTitleThenOriginalName()
	{
		var withTitle = CreateTrendingResult(originalTitle: "Original Title", originalName: "Original Name");
		var withName = CreateTrendingResult(originalTitle: null, originalName: "Original Name");

		await Assert.That(withTitle.DisplayOriginalTitle).IsEqualTo("Original Title");
		await Assert.That(withName.DisplayOriginalTitle).IsEqualTo("Original Name");
	}

	[Test]
	public async Task TrendingResult_AvailableDate_UsesFirstAirDateThenReleaseDate()
	{
		var withFirstAirDate = CreateTrendingResult(firstAirDate: "2020-01-01", releaseDate: "2019-01-01");
		var withReleaseDate = CreateTrendingResult(firstAirDate: null, releaseDate: "2019-01-01");

		await Assert.That(withFirstAirDate.AvailableDate).IsEqualTo("2020-01-01");
		await Assert.That(withReleaseDate.AvailableDate).IsEqualTo("2019-01-01");
	}

	[Test]
	public async Task Details_DisplayTitle_UsesTitleThenName()
	{
		var withTitle = new TmdbTypes.Details(1, "Original", "Original Name", "2020-01-01", null, "Title", "Name");
		var withName = new TmdbTypes.Details(1, "Original", "Original Name", "2020-01-01", null, null, "Name");

		await Assert.That(withTitle.DisplayTitle).IsEqualTo("Title");
		await Assert.That(withName.DisplayTitle).IsEqualTo("Name");
	}

	[Test]
	public async Task Details_TmdbId_ReturnsExpectedValueObject()
	{
		var testee = new TmdbTypes.Details(42, "Original", "Original Name", "2020-01-01", null, "Title", "Name");

		await Assert.That(testee.TmdbId).IsEqualTo(TmdbId.From(42));
	}

	[Test]
	public async Task Details_DisplayOriginalTitle_UsesOriginalTitleThenOriginalName()
	{
		var withOriginalTitle = new TmdbTypes.Details(1, "Original Title", "Original Name", null, null, null, "Name");
		var withOriginalName = new TmdbTypes.Details(1, null, "Original Name", null, null, null, "Name");

		await Assert.That(withOriginalTitle.DisplayOriginalTitle).IsEqualTo("Original Title");
		await Assert.That(withOriginalName.DisplayOriginalTitle).IsEqualTo("Original Name");
	}

	[Test]
	public async Task Details_DisplayTitle_WhenBothNull_ThrowsInvalidOperationException()
	{
		var testee = new TmdbTypes.Details(1, "Original", "Original Name", null, null, null, null);

		await Assert.That(() => testee.DisplayTitle).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Details_DisplayOriginalTitle_WhenBothNull_ThrowsInvalidOperationException()
	{
		var testee = new TmdbTypes.Details(1, null, null, null, null, "Title", null);

		await Assert.That(() => testee.DisplayOriginalTitle).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Details_AvailableDate_UsesFirstAirDateThenReleaseDate()
	{
		var withFirstAirDate = new TmdbTypes.Details(1, null, null, "2019-01-01", "2020-01-01", "Title", null);
		var withReleaseDate = new TmdbTypes.Details(1, null, null, "2019-01-01", null, "Title", null);
		var withNeither = new TmdbTypes.Details(1, null, null, null, null, "Title", null);

		await Assert.That(withFirstAirDate.AvailableDate).IsEqualTo("2020-01-01");
		await Assert.That(withReleaseDate.AvailableDate).IsEqualTo("2019-01-01");
		await Assert.That(withNeither.AvailableDate).IsNull();
	}

	[Test]
	public async Task TrendingResult_TmdbId_ReturnsExpectedValueObject()
	{
		var testee = CreateTrendingResult();

		await Assert.That(testee.TmdbId).IsEqualTo(TmdbId.From(10));
	}

	[Test]
	public async Task TrendingResult_AvailableDate_WhenBothNull_ReturnsNull()
	{
		var testee = CreateTrendingResult(firstAirDate: null, releaseDate: null);

		await Assert.That(testee.AvailableDate).IsNull();
	}

	private static TmdbTypes.Trending.Result CreateTrendingResult(
		string? title = "Title",
		string? name = "Name",
		string? originalTitle = "Original Title",
		string? originalName = "Original Name",
		string? releaseDate = "2019-01-01",
		string? firstAirDate = "2020-01-01"
	)
	{
		return new TmdbTypes.Trending.Result(
			Adult: false,
			BackdropPath: "/backdrop.jpg",
			Id: 10,
			Title: title,
			Name: name,
			OriginalTitle: originalTitle,
			OriginalName: originalName,
			Overview: "overview",
			PosterPath: "/poster.jpg",
			MediaType: "movie",
			OriginalLanguage: "en",
			GenreIds: [1, 2],
			Popularity: 1.0,
			ReleaseDate: releaseDate,
			FirstAirDate: firstAirDate,
			Video: false,
			VoteAverage: 8.4,
			VoteCount: 100
		);
	}
}
