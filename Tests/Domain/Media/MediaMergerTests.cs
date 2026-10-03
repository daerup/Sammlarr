using Domain.Media;
using Domain.Providers;

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
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.8f)]),
			Runtime = TimeSpan.FromMinutes(155),
		};

		var merged = first.FillFrom(second);

		await Assert.That(merged.Missing).IsEqualTo(MediaField.None);
	}

	[Test]
	public async Task FillFrom_WhenBothSidesLackAField_KeepsTheGap()
	{
		var first = new MovieDetails { Title = "Dune" };
		var second = new MovieDetails { Summary = "Paul Atreides." };

		var merged = first.FillFrom(second);

		await Assert.That(merged.Missing.Overlaps(MediaField.Runtime)).IsTrue();
	}

	[Test]
	public async Task FillFrom_WhenBothSidesHaveAValue_KeepsThePrimaryValue()
	{
		var first = new MovieDetails { Title = "Dune", Tagline = "Fear is the mind-killer" };
		var second = new MovieDetails
		{
			Title = "Wrong",
			Tagline = "Wrong tagline",
			Summary = "Paul Atreides.",
		};

		var merged = first.FillFrom(second);

		await Assert.That(merged.Title).IsEqualTo("Dune");
		await Assert.That(merged.Tagline).IsEqualTo("Fear is the mind-killer");
		await Assert.That(merged.Summary).IsEqualTo("Paul Atreides.");
	}

	[Test]
	public async Task FillFrom_WhenBothSidesCarryEveryField_KeepsThePrimaryUnchanged()
	{
		var primary = MediaFakes.CompleteMovieDetails();

		var merged = primary.FillFrom(MediaFakes.CompleteMovieDetails());

		await Assert.That(merged).IsEqualTo(primary);
	}

	[Test]
	public async Task FillFrom_WhenThePrimaryLacksTheTitle_TakesItFromTheOther()
	{
		var first = new MovieDetails { Summary = "Paul Atreides." };
		var second = new MovieDetails { Title = "Dune" };

		var merged = first.FillFrom(second);

		await Assert.That(merged.Title).IsEqualTo("Dune");
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
}
