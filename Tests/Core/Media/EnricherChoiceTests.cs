using Core.Media;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Media;

public class EnricherChoiceTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly ProviderLoad Idle = new(new None(), 0);

	[Test]
	public async Task Next_PicksTheEnricherThatFillsTheMostMissingFields() =>
		await Assert
			.That(
				EnricherChoice
					.Next(
						MediaField.Tagline | MediaField.Summary,
						[Candidate(MediaProvider.Tvdb, MediaField.Tagline), Candidate(MediaProvider.Tmdb, MediaField.Tagline | MediaField.Summary)],
						Now
					)
					.Map(candidate => candidate.Provider)
			)
			.IsSome()
			.And.IsEqualTo(MediaProvider.Tmdb);

	[Test]
	public async Task Next_ForEqualCoverage_PicksTheLessBusyEnricher() =>
		await Assert
			.That(
				EnricherChoice
					.Next(
						MediaField.Tagline,
						[
							Candidate(MediaProvider.Tmdb, MediaField.Tagline, Idle with { Busy = 0.8 }),
							Candidate(MediaProvider.Tvdb, MediaField.Tagline, Idle with { Busy = 0.1 }),
						],
						Now
					)
					.Map(candidate => candidate.Provider)
			)
			.IsSome()
			.And.IsEqualTo(MediaProvider.Tvdb);

	[Test]
	public async Task Next_SkipsABlockedEnricherAndOneThatCannotFillAnything() =>
		await Assert
			.That(
				EnricherChoice.Next(
					MediaField.Tagline,
					[
						Candidate(MediaProvider.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) }),
						Candidate(MediaProvider.Tvdb, MediaField.Poster),
					],
					Now
				)
			)
			.IsNone();

	[Test]
	public async Task Next_ForAnEnricherWhoseBlockHasPassed_PicksItAgain() =>
		await Assert
			.That(
				EnricherChoice
					.Next(MediaField.Tagline, [Candidate(MediaProvider.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(-1)) })], Now)
					.Map(candidate => candidate.Provider)
			)
			.IsSome()
			.And.IsEqualTo(MediaProvider.Tmdb);

	[Test]
	public async Task BlockedFills_NamesTheFieldsOfTheBlockedEnrichersThatCouldHaveHelped() =>
		await Assert
			.That(
				EnricherChoice.BlockedFills(
					MediaField.Tagline,
					[
						Candidate(MediaProvider.Tmdb, MediaField.Tagline | MediaField.Summary, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) }),
						Candidate(MediaProvider.Tvdb, MediaField.Tagline),
					],
					Now
				)
			)
			.IsEqualTo(MediaField.Tagline | MediaField.Summary);

	[Test]
	public async Task Next_WhenOnlyRatingsAreMissing_PicksTheEnricherThatRatesWithTheMostSources()
	{
		var candidates = new[] { Candidate(MediaProvider.Tmdb, MediaFields.Rated([RatingSource.Tmdb])), Candidate(MediaProvider.Plex, MediaFields.AllRatings) };

		var next = EnricherChoice.Next(MediaFields.AllRatings, candidates, Now);

		await Assert.That(next.Map(candidate => candidate.Provider)).IsSome().And.IsEqualTo(MediaProvider.Plex);
	}

	[Test]
	public async Task CanHelp_WhenItFillsNothingNeeded_IsFalse()
	{
		await Assert.That(Candidate(MediaProvider.Tmdb, MediaField.Poster).CanHelp(MediaField.Tagline, Now)).IsFalse();
	}

	[Test]
	public async Task CanHelp_WhenItIsBlocked_IsFalse()
	{
		var blocked = Candidate(MediaProvider.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) });

		await Assert.That(blocked.CanHelp(MediaField.Tagline, Now)).IsFalse();
	}

	[Test]
	public async Task CanHelp_WhenItIsOpenAndFillsANeededField_IsTrue()
	{
		await Assert.That(Candidate(MediaProvider.Plex, MediaFields.AllRatings).CanHelp(MediaFields.Rated([RatingSource.Imdb]), Now)).IsTrue();
	}

	private static EnricherCandidate Candidate(MediaProvider provider, MediaFields fills, ProviderLoad? load = null) =>
		new(provider, TmdbId.From("1", MediaType.Movie), fills, load ?? Idle);
}
