using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using TestInfrastructure;

namespace Domain.Tests.Media.Metadata;

public class MediaMergerTests
{
	[Test]
	public async Task FromMedia_WhenComplete_PromotesToMovie()
	{
		IPartialMedia partial = MediaFakes.CompletePartialMovie();

		var match = IdentifiedMedia.From(partial);

		await Assert.That(match).IsOfUnionType<IdentifiedMedia, Movie>();
	}

	[Test]
	public async Task FromMedia_WhenIncomplete_StaysPartial()
	{
		IPartialMedia partial = new PartialMovie { Identity = MediaFakes.MovieIdentity(), Title = "Dune" };

		var match = IdentifiedMedia.From(partial);

		await Assert.That(match).IsOfUnionType<IdentifiedMedia, PartialMovie>();
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
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.8f)]),
			Runtime = TimeSpan.FromMinutes(155),
		};

		var merged = first.MergeWith(second);

		await Assert.That(merged).IsOfUnionType<IdentifiedMedia, Movie>();
	}

	[Test]
	public async Task MergeWith_WhenStillIncomplete_StaysPartial()
	{
		var identity = MediaFakes.MovieIdentity();
		IPartialMedia first = new PartialMovie { Identity = identity, Title = "Dune" };
		IPartialMedia second = new PartialMovie { Identity = identity, Summary = "Paul Atreides." };

		var merged = first.MergeWith(second);

		await Assert.That(merged).IsOfUnionType<IdentifiedMedia, PartialMovie>();
	}

	[Test]
	public async Task MergeWith_PrefersExistingValueOverOther()
	{
		var identity = MediaFakes.MovieIdentity();
		IPartialMedia first = new PartialMovie
		{
			Identity = identity,
			Title = "Dune",
			Tagline = "Fear is the mind-killer",
		};
		IPartialMedia second = new PartialMovie
		{
			Identity = identity,
			Title = "Wrong",
			Tagline = "Wrong tagline",
			Summary = "Paul Atreides.",
		};

		var merged = first.MergeWith(second);

		await Assert
			.That(merged)
			.IsOfUnionType<IdentifiedMedia, PartialMovie>()
			.And.Multiple(async movie =>
			{
				await Assert.That(movie.Title).IsEqualTo("Dune");
				await Assert.That(movie.Tagline).IsEqualTo("Fear is the mind-killer");
			});
	}

	[Test]
	public async Task MergeWith_WhenBothSidesCarryEveryField_PrefersThePartialOnAllOfThem()
	{
		var source = MediaFakes.CompletePartialMovie();
		IPartialMedia partial = source;
		IMedia complete = MediaFakes.CompleteMovie();

		var merged = partial.MergeWith(complete);

		await Assert
			.That(merged)
			.IsOfUnionType<IdentifiedMedia, Movie>()
			.And.Multiple(async movie =>
			{
				await Assert.That(movie.Title).IsEqualTo(source.Title);
				await Assert.That(movie.Tagline).IsEqualTo(source.Tagline);
				await Assert.That(movie.Summary).IsEqualTo(source.Summary);
				await Assert.That(movie.PosterUrl).IsEqualTo(source.PosterUrl);
				await Assert.That(movie.BackdropUrl).IsEqualTo(source.BackdropUrl);
				await Assert.That(movie.ReleaseDate).IsEqualTo(source.ReleaseDate);
				await Assert.That(movie.Runtime).IsEqualTo(source.Runtime);
			});
	}

	[Test]
	public async Task MergeWith_WhenThePartialLacksTheTitle_TakesItFromTheOther()
	{
		var identity = MediaFakes.MovieIdentity();
		IPartialMedia first = new PartialMovie { Identity = identity, Summary = "Paul Atreides." };
		IPartialMedia second = new PartialMovie { Identity = identity, Title = "Dune" };

		var merged = first.MergeWith(second);

		await Assert.That(merged).IsOfUnionType<IdentifiedMedia, PartialMovie>().And.Multiple(async movie => await Assert.That(movie.Title).IsEqualTo("Dune"));
	}

	[Test]
	public async Task MergeWith_WhenOtherIsCompleteMedia_FillsAndPromotes()
	{
		IPartialMedia partial = new PartialMovie { Identity = MediaFakes.MovieIdentity(), Title = "Custom" };
		IMedia complete = MediaFakes.CompleteMovie();

		var merged = partial.MergeWith(complete);

		await Assert.That(merged).IsOfUnionType<IdentifiedMedia, Movie>().And.Multiple(async movie => await Assert.That(movie.Title).IsEqualTo("Custom"));
	}

	[Test]
	public async Task MergeWith_WhenAProviderAnswersACompleteMovie_KeepsThePartialsMediaId()
	{
		var identity = MediaFakes.MovieIdentity();
		IPartialMedia partial = new PartialMovie { Identity = identity, Title = "Custom" };
		var answer = MediaFakes.CompleteMovie();
		IMedia complete = answer with { Identity = answer.Identity with { Id = default } };

		var merged = partial.MergeWith(complete);

		await Assert.That(merged.Identity.Id).IsEqualTo(identity.Id);
		await Assert.That(merged.Identity.Externals).IsEquivalentTo([.. identity.Externals, .. answer.Identity.Externals]);
	}

	[Test]
	public async Task MergeWith_WhenAProviderAnswersACompleteShow_KeepsThePartialsMediaId()
	{
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("1396", MediaType.Show)]);
		IPartialMedia partial = new PartialShow { Identity = identity, Title = "Breaking Bad" };
		IMedia complete = new Show
		{
			Identity = new MediaIdentity(default, [TvdbId.From("81189", MediaType.Show)]),
			Title = "Breaking Bad",
			Tagline = "Say my name.",
			Summary = "A chemistry teacher turns to crime.",
			PosterUrl = "/poster.jpg",
			BackdropUrl = "/backdrop.jpg",
			ReleaseDate = new DateOnly(2008, 1, 20),
			Genres = ["Drama"],
			Rating = RatingSummary.None,
			Seasons = 5,
			Episodes = 62,
			RuntimePerEpisode = TimeSpan.FromMinutes(47),
		};

		var merged = partial.MergeWith(complete);

		await Assert.That(merged.Identity.Id).IsEqualTo(identity.Id);
		await Assert
			.That(merged.Identity.Externals)
			.IsEquivalentTo(new IExternalMediaId[] { TmdbId.From("1396", MediaType.Show), TvdbId.From("81189", MediaType.Show) });
	}

	[Test]
	public async Task MergeWith_WhenOtherIsIdentifiedComplete_DispatchesToMediaOverload()
	{
		IPartialMedia partial = new PartialMovie { Identity = MediaFakes.MovieIdentity(), Title = "Custom" };
		IdentifiedMedia other = MediaFakes.CompleteMovie();

		var merged = partial.MergeWith(other);

		await Assert.That(merged).IsOfUnionType<IdentifiedMedia, Movie>().And.Multiple(async movie => await Assert.That(movie.Title).IsEqualTo("Custom"));
	}

	[Test]
	public async Task MergeWith_WhenOtherIsIdentifiedPartial_DispatchesToPartialOverload()
	{
		var identity = MediaFakes.MovieIdentity();
		IPartialMedia partial = new PartialMovie { Identity = identity, Title = "Dune" };
		IdentifiedMedia other = IdentifiedMedia.From(new PartialMovie { Identity = identity, Summary = "Paul Atreides." });

		var merged = partial.MergeWith(other);

		await Assert.That(merged).IsOfUnionType<IdentifiedMedia, PartialMovie>().And.Multiple(async movie => await Assert.That(movie.Title).IsEqualTo("Dune"));
	}
}
