using CsCheck;
using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class ProviderAnswerTests
{
	private static readonly DateTimeOffset At = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

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
				Ratings = MediaRatings.From([.. rated.Select(source => source.Card.Rate(8, new None()))]),
			}
	);

	[Test]
	public async Task After_NamesWhatTheProviderFillsAndItsAnswerLacks()
	{
		var answered = new MovieDetails { Title = "Dune", Ratings = MediaRatings.From([new TmdbRating(7.8f, Some.Of(12_000))]) };

		var answer = ProviderAnswer.After(RemoteService.Plex, MediaField.Tagline | MediaField.Summary | MediaFields.AllRatings, answered, At);

		await Assert
			.That(answer.Unfilled)
			.IsEqualTo(
				MediaField.Tagline
					| MediaField.Summary
					| MediaFields.Rated([RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience])
			);
		await Assert.That(answer.AnsweredAt).IsEqualTo(At);
	}

	[Test]
	public void After_ForAnyFillsAndAnswer_NeverNamesAFieldOutsideTheFills()
	{
		Gen.Select(MediaFieldsGen.Fields, Titles)
			.Sample((fills, answered) => ProviderAnswer.After(RemoteService.Tmdb, fills, answered, At).Unfilled.Except(fills).IsEmpty);
	}

	[Test]
	public void After_ForAnyFillsAndAnswer_NeverNamesAFieldTheAnswerHas()
	{
		Gen.Select(MediaFieldsGen.Fields, Titles)
			.Sample((fills, answered) => ProviderAnswer.After(RemoteService.Tmdb, fills, answered, At).Unfilled.Except(answered.Missing).IsEmpty);
	}
}
