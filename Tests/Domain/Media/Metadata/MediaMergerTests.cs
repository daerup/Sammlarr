namespace Domain.Tests.Media.Metadata;

public class MediaMergerTests
{
	[Test]
	public async Task FromMedia_WhenComplete_PromotesToMovie()
	{
		IPartialMedia partial = MediaFakes.CompletePartialMovie();

		var match = MediaMatch.FromMedia(partial);

		await Assert.That(match is Movie).IsTrue();
	}

	[Test]
	public async Task FromMedia_WhenIncomplete_StaysPartial()
	{
		IPartialMedia partial = new PartialMovie { Identity = MediaFakes.MovieIdentity(), Title = "Dune" };

		var match = MediaMatch.FromMedia(partial);

		await Assert.That(match is PartialMovie).IsTrue();
	}

	[Test]
	public async Task MergeWith_WhenGapsFilled_PromotesToMovie()
	{
		var identity = MediaFakes.MovieIdentity();
		IPartialMedia first = new PartialMovie
		{
			Identity = identity,
			Title = "Dune",
			Tagline = "Fear is the mind-killer",
			Summary = "Paul Atreides.",
			Genres = ["Science Fiction"],
		};
		IPartialMedia second = new PartialMovie
		{
			Identity = identity,
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2021, 10, 22),
			Runtime = TimeSpan.FromMinutes(155),
		};

		var merged = first.MergeWith(second);

		await Assert.That(merged is Movie).IsTrue();
	}

	[Test]
	public async Task MergeWith_WhenStillIncomplete_StaysPartial()
	{
		var identity = MediaFakes.MovieIdentity();
		IPartialMedia first = new PartialMovie { Identity = identity, Title = "Dune" };
		IPartialMedia second = new PartialMovie { Identity = identity, Summary = "Paul Atreides." };

		var merged = first.MergeWith(second);

		await Assert.That(merged is PartialMovie).IsTrue();
	}

	[Test]
	public async Task MergeWith_PrefersExistingValueOverOther()
	{
		var identity = MediaFakes.MovieIdentity();
		IPartialMedia first = new PartialMovie { Identity = identity, Title = "Dune" };
		IPartialMedia second = new PartialMovie
		{
			Identity = identity,
			Title = "Wrong",
			Summary = "Paul Atreides.",
		};

		var merged = first.MergeWith(second);

		await Assert.That(merged is PartialMovie movie && movie.Title == "Dune").IsTrue();
	}

	[Test]
	public async Task WithRatings_WhenPartial_AppliesRating()
	{
		IPartialMedia partial = new PartialMovie { Identity = MediaFakes.MovieIdentity(), Title = "Dune" };

		var withRatings = MediaMerger.WithRatings(partial, [new MediaRating(RatingSource.Tmdb, 7f)]);

		await Assert.That(withRatings is PartialMovie movie && movie.Rating.Average == 7f).IsTrue();
	}

	[Test]
	public async Task MergeWith_WhenOtherIsCompleteMedia_FillsAndPromotes()
	{
		IPartialMedia partial = new PartialMovie { Identity = MediaFakes.MovieIdentity(), Title = "Custom" };
		IMedia complete = MediaFakes.CompleteMovie();

		var merged = partial.MergeWith(complete);

		await Assert.That(merged is Movie movie && movie.Title == "Custom").IsTrue();
	}
}
