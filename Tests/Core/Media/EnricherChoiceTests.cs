using Core.Media;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Media;

public class EnricherChoiceTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly ProviderLoad Idle = new(new None(), 0);
	private static readonly IReadOnlySet<MediaProvider> NoneWaited = new HashSet<MediaProvider>();
	private static readonly MediaFields PlexFills = MediaField.Tagline | MediaFields.AllRatings;

	private static readonly MovieDetails Complete = new()
	{
		Title = "Dune",
		Tagline = "Fear is the mind-killer.",
		Summary = "A noble family becomes embroiled in a war.",
		PosterUrl = "poster",
		BackdropUrl = "backdrop",
		ReleaseDate = new DateOnly(2021, 10, 22),
		Genres = ["Science Fiction"],
		Ratings = MediaRatings.From([
			new TmdbRating(7.8f, Some.Of(12_000)),
			new ImdbRating(8.0f, Some.Of(900_000)),
			new RottenTomatoesCriticsRating(83),
			new RottenTomatoesAudienceRating(90),
		]),
		Runtime = TimeSpan.FromMinutes(155),
	};

	[Test]
	public async Task Next_PicksTheEnricherThatFillsTheMostMissingFields() =>
		await Assert
			.That(
				EnricherChoice
					.Next(
						MediaField.Tagline | MediaField.Summary,
						[Candidate(MediaProvider.Tmdb, MediaField.Tagline), Candidate(MediaProvider.Tvdb, MediaField.Tagline | MediaField.Summary)],
						new None(),
						Now
					)
					.Map(candidate => candidate.Provider)
			)
			.IsSome()
			.And.IsEqualTo(MediaProvider.Tvdb);

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
						new None(),
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
					new None(),
					Now
				)
			)
			.IsNone();

	[Test]
	public async Task Next_ForAnEnricherWhoseBlockHasPassed_PicksItAgain() =>
		await Assert
			.That(
				EnricherChoice
					.Next(
						MediaField.Tagline,
						[Candidate(MediaProvider.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(-1)) })],
						new None(),
						Now
					)
					.Map(candidate => candidate.Provider)
			)
			.IsSome()
			.And.IsEqualTo(MediaProvider.Tmdb);

	[Test]
	public async Task WorthWaitingFor_WhenNoneIsBlocked_PicksTheOneThatFillsAMissingField()
	{
		var picked = EnricherChoice.WorthWaitingFor(
			MediaField.Tagline,
			[Candidate(MediaProvider.Tvdb, MediaField.Poster), Candidate(MediaProvider.Tmdb, MediaField.Tagline)],
			NoneWaited,
			Now
		);

		await Assert.That(picked.Map(candidate => candidate.Provider)).IsSome().And.IsEqualTo(MediaProvider.Tmdb);
	}

	[Test]
	public async Task WorthWaitingFor_WhenTheBlockEndsWithinTheEnrichWait_PicksIt()
	{
		var picked = EnricherChoice.WorthWaitingFor(MediaField.Tagline, [Candidate(MediaProvider.Tmdb, MediaField.Tagline, Blocked(seconds: 5))], NoneWaited, Now);

		await Assert.That(picked.Map(candidate => candidate.Provider)).IsSome().And.IsEqualTo(MediaProvider.Tmdb);
	}

	[Test]
	public async Task WorthWaitingFor_WhenTheBlockOutlastsTheEnrichWait_IsNone()
	{
		await Assert
			.That(EnricherChoice.WorthWaitingFor(MediaField.Tagline, [Candidate(MediaProvider.Tmdb, MediaField.Tagline, Blocked(seconds: 30))], NoneWaited, Now))
			.IsNone();
	}

	[Test]
	public async Task WorthWaitingFor_WhenItHasNoEnrichWait_IsNoneEvenForAShortBlock()
	{
		var unbudgeted = Idle with { BlockedUntil = Some.Of(Now.AddSeconds(1)) };

		await Assert
			.That(EnricherChoice.WorthWaitingFor(MediaField.Tagline, [Candidate(MediaProvider.Plex, MediaField.Tagline, unbudgeted)], NoneWaited, Now))
			.IsNone();
	}

	[Test]
	public async Task WorthWaitingFor_WhenItWasWaitedForAlready_IsNone()
	{
		await Assert
			.That(
				EnricherChoice.WorthWaitingFor(
					MediaField.Tagline,
					[Candidate(MediaProvider.Tmdb, MediaField.Tagline, Blocked(seconds: 5))],
					new HashSet<MediaProvider> { MediaProvider.Tmdb },
					Now
				)
			)
			.IsNone();
	}

	[Test]
	public async Task WorthWaitingFor_OfTwoBlocked_PicksTheSoonestFree()
	{
		var picked = EnricherChoice.WorthWaitingFor(
			MediaField.Tagline,
			[Candidate(MediaProvider.Tmdb, MediaField.Tagline, Blocked(seconds: 10)), Candidate(MediaProvider.Plex, MediaField.Tagline, Blocked(seconds: 3))],
			NoneWaited,
			Now
		);

		await Assert.That(picked.Map(candidate => candidate.Provider)).IsSome().And.IsEqualTo(MediaProvider.Plex);
	}

	[Test]
	public async Task NeedsRefetch_WhenNobodyLeftToAskCanFillAGap_IsFalse()
	{
		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [], Now)).IsFalse();
	}

	[Test]
	public async Task NeedsRefetch_WhenANewlyConnectedEnricherFillsAGap_IsTrue()
	{
		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [Candidate(MediaProvider.Plex, PlexFills)], Now)).IsTrue();
	}

	[Test]
	public async Task NeedsRefetch_WhenTheOnlyEnricherThatFillsAGapIsBlocked_IsFalse()
	{
		var blocked = Candidate(MediaProvider.Plex, PlexFills, Idle with { BlockedUntil = Some.Of(Now.AddMinutes(1)) });

		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [blocked], Now)).IsFalse();
	}

	[Test]
	public async Task NeedsRefetch_OnceTheBlockHasLifted_IsTrue()
	{
		var lifted = Candidate(MediaProvider.Plex, PlexFills, Idle with { BlockedUntil = Some.Of(Now.AddMinutes(-1)) });

		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [lifted], Now)).IsTrue();
	}

	[Test]
	public async Task NeedsRefetch_WithinAnHourOfTheLastAsk_WaitsEvenThoughSomeoneNewCouldFillAGap()
	{
		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromMinutes(30)), [Candidate(MediaProvider.Plex, PlexFills)], Now)).IsFalse();
	}

	[Test]
	public async Task NeedsRefetch_WhenNothingIsMissing_IsFalse()
	{
		var complete = Dune(askedAgo: TimeSpan.FromDays(1)) with { Details = Complete };

		await Assert.That(EnricherChoice.NeedsRefetch(complete, [Candidate(MediaProvider.Plex, PlexFills)], Now)).IsFalse();
	}

	[Test]
	public async Task NeedsRefetch_TwoWeeksAfterTheRefresh_IsTrueEvenRightAfterAnAskWithNobodyLeft()
	{
		var stale = Dune(askedAgo: TimeSpan.FromMinutes(1)) with { RefreshedAt = Now.AddDays(-14) };

		await Assert.That(EnricherChoice.NeedsRefetch(stale, [], Now)).IsTrue();
	}

	[Test]
	public async Task Next_WhenOnlyRatingsAreMissing_CountsNoRatingsAndFallsBackToProviderOrder()
	{
		var candidates = new[] { Candidate(MediaProvider.Plex, MediaFields.AllRatings), Candidate(MediaProvider.Tmdb, MediaFields.Rated([RatingSource.Tmdb])) };

		var next = EnricherChoice.Next(MediaFields.AllRatings, candidates, new None(), Now);

		await Assert.That(next.Map(candidate => candidate.Provider)).IsSome().And.IsEqualTo(MediaProvider.Tmdb);
	}

	[Test]
	public async Task Next_WhenThePreferredProviderCanHelp_PicksItOverOneThatFillsMore()
	{
		var candidates = new[] { Candidate(MediaProvider.Tmdb, MediaField.Tagline | MediaField.Summary), Candidate(MediaProvider.Plex, MediaField.Tagline) };

		var next = EnricherChoice.Next(MediaField.Tagline | MediaField.Summary, candidates, Some.Of(MediaProvider.Plex), Now);

		await Assert.That(next.Map(candidate => candidate.Provider)).IsSome().And.IsEqualTo(MediaProvider.Plex);
	}

	[Test]
	public async Task Next_WhenThePreferredProviderIsBlocked_FallsThroughToTheOther()
	{
		var candidates = new[]
		{
			Candidate(MediaProvider.Plex, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) }),
			Candidate(MediaProvider.Tmdb, MediaField.Tagline),
		};

		var next = EnricherChoice.Next(MediaField.Tagline, candidates, Some.Of(MediaProvider.Plex), Now);

		await Assert.That(next.Map(candidate => candidate.Provider)).IsSome().And.IsEqualTo(MediaProvider.Tmdb);
	}

	[Test]
	public async Task Next_ForEqualFieldsAndLoad_PicksTmdbBeforePlex()
	{
		var candidates = new[] { Candidate(MediaProvider.Plex, MediaField.Tagline), Candidate(MediaProvider.Tmdb, MediaField.Tagline) };

		var next = EnricherChoice.Next(MediaField.Tagline, candidates, new None(), Now);

		await Assert.That(next.Map(candidate => candidate.Provider)).IsSome().And.IsEqualTo(MediaProvider.Tmdb);
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

	private static ProviderLoad Blocked(int seconds) => Idle with { BlockedUntil = Some.Of(Now.AddSeconds(seconds)), EnrichWait = TimeSpan.FromSeconds(15) };

	private static CachedDetails Dune(TimeSpan askedAgo)
	{
		var details = new MovieDetails { Title = "Dune", Ratings = MediaRatings.From([new TmdbRating(7.8f, Some.Of(12_000))]) };
		return new CachedDetails(details, Now.AddDays(-3), Now - askedAgo, [new ProviderAnswer(MediaProvider.Tmdb, MediaField.Tagline)]);
	}

	private static EnricherCandidate Candidate(MediaProvider provider, MediaFields fills, ProviderLoad? load = null) =>
		new(provider, TmdbId.From("1", MediaType.Movie), fills, load ?? Idle);
}
