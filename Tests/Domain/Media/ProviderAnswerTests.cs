using CsCheck;
using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class ProviderAnswerTests
{
	private static readonly DateTimeOffset At = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan FreshFor = TimeSpan.FromDays(14);

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

		var answer = ProviderAnswer.After(
			RemoteService.Plex,
			MediaField.Tagline | MediaField.Summary | MediaFields.Rated(RemoteService.Plex.SuppliedRatingSources()),
			answered,
			At
		);

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

	[Test]
	public async Task AskableAt_ForAnAnswer_IsTwoWeeksLater()
	{
		await Assert.That(new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, At).AskableAt(FreshFor)).IsEqualTo(At.AddDays(14));
	}

	[Test]
	public async Task AskableAt_ForAnAnswer_IsTheFreshPeriodLater()
	{
		await Assert.That(new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, At).AskableAt(TimeSpan.FromDays(7))).IsEqualTo(At.AddDays(7));
	}

	[Test]
	public async Task Failed_AfterAnAnswer_StartsTheBackoffAtAnHour()
	{
		var failed = ProviderAnswer.Failed(RemoteService.Tmdb, Some.Of(new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, At.AddDays(-1))), At);

		await Assert.That(failed.Failures).IsEqualTo(1);
		await Assert.That(failed.AskableAt(FreshFor)).IsEqualTo(At.AddHours(1));
	}

	[Test]
	public async Task Failed_ForTheThirdTimeAndLater_WaitsADay()
	{
		var second = ProviderAnswer.Failed(RemoteService.Tmdb, Some.Of(ProviderAnswer.Failed(RemoteService.Tmdb, new None(), At)), At);
		var third = ProviderAnswer.Failed(RemoteService.Tmdb, Some.Of(second), At);
		var fifth = ProviderAnswer.Failed(RemoteService.Tmdb, Some.Of(ProviderAnswer.Failed(RemoteService.Tmdb, Some.Of(third), At)), At);

		await Assert.That(second.AskableAt(FreshFor)).IsEqualTo(At.AddHours(6));
		await Assert.That(third.AskableAt(FreshFor)).IsEqualTo(At.AddHours(24));
		await Assert.That(fifth.AskableAt(FreshFor)).IsEqualTo(At.AddHours(24));
	}
}
