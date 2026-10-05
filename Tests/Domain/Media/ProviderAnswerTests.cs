using CsCheck;
using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class ProviderAnswerTests
{
	private static readonly Gen<MovieDetails> Titles = Gen.Select(
		Gen.Bool.Array[3],
		MediaFieldsGen.RatingSources,
		(present, rated) =>
			new MovieDetails
			{
				Title = "Dune",
				Tagline = present[0] ? "Fear is the mind-killer." : null,
				Summary = present[1] ? "A noble family becomes embroiled in a war." : null,
				Runtime = present[2] ? TimeSpan.FromMinutes(155) : null,
				Ratings = MediaRatings.From([.. rated.Select(source => source.Rate(8, new None()))]),
			}
	);

	[Test]
	public async Task After_NamesWhatTheProviderFillsAndTheTitleStillLacks()
	{
		var merged = new MovieDetails { Title = "Dune", Ratings = MediaRatings.From([new TmdbRating(7.8f, Some.Of(12_000))]) };

		var answer = ProviderAnswer.After(MediaProvider.Plex, MediaField.Tagline | MediaField.Summary | MediaFields.AllRatings, merged);

		await Assert
			.That(answer.Unfilled)
			.IsEqualTo(
				MediaField.Tagline
					| MediaField.Summary
					| MediaFields.Rated([RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience])
			);
	}

	[Test]
	public void After_ForAnyFillsAndTitle_NeverNamesAFieldOutsideTheFills()
	{
		Gen.Select(MediaFieldsGen.Fields, Titles).Sample((fills, merged) => ProviderAnswer.After(MediaProvider.Tmdb, fills, merged).Unfilled.Except(fills).IsEmpty);
	}

	[Test]
	public void After_ForAnyFillsAndTitle_NeverNamesAFieldTheTitleHas()
	{
		Gen.Select(MediaFieldsGen.Fields, Titles)
			.Sample((fills, merged) => ProviderAnswer.After(MediaProvider.Tmdb, fills, merged).Unfilled.Except(merged.Missing).IsEmpty);
	}
}
