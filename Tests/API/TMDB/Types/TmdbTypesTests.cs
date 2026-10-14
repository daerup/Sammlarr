using API.Tmdb.Types;
using Domain.Media;

namespace API.Tests.TMDB.Types;

public class TmdbTypesTests
{
	[Test]
	public async Task TrendingResult_DisplayTitle_UsesTitleThenName()
	{
		var withTitle = CreateTrendingResult();
		var withName = CreateTrendingResult(null);

		await Assert.That(withTitle.DisplayTitle).IsEqualTo("Title");
		await Assert.That(withName.DisplayTitle).IsEqualTo("Name");
	}

	[Test]
	public async Task TrendingResult_DisplayTitle_WhenTheTitleIsBlank_UsesTheName()
	{
		var testee = CreateTrendingResult(" ");

		await Assert.That(testee.DisplayTitle).IsEqualTo("Name");
	}

	[Test]
	public async Task TrendingResult_DisplayTitle_WhenBothAreMissing_IsNull()
	{
		var testee = CreateTrendingResult(null, null);

		await Assert.That(testee.DisplayTitle).IsNull();
	}

	[Test]
	public async Task TrendingResult_Released_ForAMovie_ReadsTheReleaseDateFirst()
	{
		var testee = CreateTrendingResult(firstAirDate: "2020-01-01", releaseDate: "2019-01-01");

		await Assert.That(testee.Released).IsEqualTo(new DateOnly(2019, 1, 1));
	}

	[Test]
	public async Task TrendingResult_Released_ForAShow_ReadsTheFirstAirDateFirst()
	{
		var testee = CreateTrendingResult(firstAirDate: "2020-01-01", releaseDate: "2019-01-01", type: "tv");

		await Assert.That(testee.Released).IsEqualTo(new DateOnly(2020, 1, 1));
	}

	[Test]
	public async Task TrendingResult_Released_WhenOnlyTheOtherDateIsKnown_FallsBackToIt()
	{
		var testee = CreateTrendingResult(firstAirDate: "2020-01-01", releaseDate: null);

		await Assert.That(testee.Released).IsEqualTo(new DateOnly(2020, 1, 1));
	}

	[Test]
	public async Task TrendingResult_Released_WhenBothDatesAreMissingOrBlank_IsNull()
	{
		var testee = CreateTrendingResult(firstAirDate: "", releaseDate: null);

		await Assert.That(testee.Released).IsNull();
	}

	[Test]
	public async Task Details_ReleasedAs_ReadsTheTypesOwnDateFirst()
	{
		var testee = new TmdbTypes.Details(1, null, null, "2019-01-01", "2020-01-01", "Title", null, "unknown");

		await Assert.That(testee.ReleasedAs(MediaType.Show)).IsEqualTo(new DateOnly(2020, 1, 1));
		await Assert.That(testee.ReleasedAs(MediaType.Movie)).IsEqualTo(new DateOnly(2019, 1, 1));
	}

	private static TmdbTypes.Trending.Result CreateTrendingResult(
		string? title = "Title",
		string? name = "Name",
		string? releaseDate = "2019-01-01",
		string? firstAirDate = "2020-01-01",
		string type = "movie"
	)
	{
		return new(
			Adult: false,
			BackdropPath: "/backdrop.jpg",
			Id: 10,
			Title: title,
			Name: name,
			OriginalTitle: "Original Title",
			OriginalName: "Original Name",
			Overview: "overview",
			PosterPath: "/poster.jpg",
			Type: type,
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
