using CsCheck;
using Domain.Media;

namespace Domain.Tests.Media;

public class MediaMergerProperties
{
	private static readonly Gen<MovieDetails> Primaries = Gen.Bool.Array[9].Select(flags => Build(flags, "primary"));
	private static readonly Gen<MovieDetails> Others = Gen.Bool.Array[9].Select(flags => Build(flags, "other"));

	[Test]
	public void FillFrom_ForAnyTwoMovies_TakesEachFieldFromThePrimaryFirst()
	{
		Gen.Select(Primaries, Others).Sample((primary, other) => primary.FillFrom(other) == Expected(primary, other));
	}

	[Test]
	public void FillFrom_WhenFillingGaps_NeverRemovesData()
	{
		Gen.Select(Primaries, Others).Sample((primary, other) => !primary.FillFrom(other).Missing.Overlaps(~primary.Missing));
	}

	[Test]
	public void FillFrom_WhenRepeated_ChangesNothing()
	{
		Gen.Select(Primaries, Others).Sample((primary, other) => primary.FillFrom(other).FillFrom(other) == primary.FillFrom(other));
	}

	[Test]
	public void FillFrom_WhenTheOtherIsAShow_LeavesTheMovieUnchanged()
	{
		Primaries.Sample(movie => movie.FillFrom(new ShowDetails { Title = "Show", Seasons = 2 }) == movie);
	}

	private static MovieDetails Expected(MovieDetails primary, MovieDetails other)
	{
		return new()
		{
			Title = primary.Title ?? other.Title,
			Tagline = primary.Tagline ?? other.Tagline,
			Summary = primary.Summary ?? other.Summary,
			PosterUrl = primary.PosterUrl ?? other.PosterUrl,
			BackdropUrl = primary.BackdropUrl ?? other.BackdropUrl,
			ReleaseDate = primary.ReleaseDate ?? other.ReleaseDate,
			Genres = primary.Genres.Count > 0 ? primary.Genres : other.Genres,
			Rating = primary.Rating.Sources.Count > 0 ? primary.Rating : other.Rating,
			Runtime = primary.Runtime ?? other.Runtime,
		};
	}

	private static MovieDetails Build(bool[] flags, string side)
	{
		return new()
		{
			Title = flags[0] ? $"{side} title" : null,
			Tagline = flags[1] ? $"{side} tagline" : null,
			Summary = flags[2] ? $"{side} summary" : null,
			PosterUrl = flags[3] ? $"/{side}-poster.jpg" : null,
			BackdropUrl = flags[4] ? $"/{side}-backdrop.jpg" : null,
			ReleaseDate = flags[5] ? new DateOnly(2020, 1, side.Length) : null,
			Genres = flags[6] ? [side] : [],
			Rating = flags[8] ? RatingSummary.From([new TmdbRating(side.Length, new None())]) : RatingSummary.None,
			Runtime = flags[7] ? TimeSpan.FromMinutes(side.Length) : null,
		};
	}
}
