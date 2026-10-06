using Core.Media;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Media;

public class EnricherChoiceTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly RemoteLoad Idle = new(new None(), 0);
	private static readonly IReadOnlySet<RemoteService> NoneWaited = new HashSet<RemoteService>();
	private static readonly IReadOnlySet<RemoteService> NoPreference = new HashSet<RemoteService>();
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
						[Candidate(RemoteService.Tmdb, MediaField.Tagline), Candidate(RemoteService.Mdblist, MediaField.Tagline | MediaField.Summary)],
						NoPreference,
						Now
					)
					.Map(candidate => candidate.Remote)
			)
			.IsSome()
			.And.IsEqualTo(RemoteService.Mdblist);

	[Test]
	public async Task Next_ForEqualCoverage_PicksTheLessBusyEnricher() =>
		await Assert
			.That(
				EnricherChoice
					.Next(
						MediaField.Tagline,
						[
							Candidate(RemoteService.Tmdb, MediaField.Tagline, Idle with { Busy = 0.8 }),
							Candidate(RemoteService.Mdblist, MediaField.Tagline, Idle with { Busy = 0.1 }),
						],
						NoPreference,
						Now
					)
					.Map(candidate => candidate.Remote)
			)
			.IsSome()
			.And.IsEqualTo(RemoteService.Mdblist);

	[Test]
	public async Task Next_SkipsABlockedEnricherAndOneThatCannotFillAnything() =>
		await Assert
			.That(
				EnricherChoice.Next(
					MediaField.Tagline,
					[
						Candidate(RemoteService.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) }),
						Candidate(RemoteService.Mdblist, MediaField.Poster),
					],
					NoPreference,
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
						[Candidate(RemoteService.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(-1)) })],
						NoPreference,
						Now
					)
					.Map(candidate => candidate.Remote)
			)
			.IsSome()
			.And.IsEqualTo(RemoteService.Tmdb);

	[Test]
	public async Task WorthWaitingFor_WhenNoneIsBlocked_PicksTheOneThatFillsAMissingField()
	{
		var picked = EnricherChoice.WorthWaitingFor(
			MediaField.Tagline,
			[Candidate(RemoteService.Mdblist, MediaField.Poster), Candidate(RemoteService.Tmdb, MediaField.Tagline)],
			NoneWaited,
			Now
		);

		await Assert.That(picked.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Tmdb);
	}

	[Test]
	public async Task WorthWaitingFor_WhenTheBlockEndsWithinTheEnrichWait_PicksIt()
	{
		var picked = EnricherChoice.WorthWaitingFor(MediaField.Tagline, [Candidate(RemoteService.Tmdb, MediaField.Tagline, Blocked(seconds: 5))], NoneWaited, Now);

		await Assert.That(picked.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Tmdb);
	}

	[Test]
	public async Task WorthWaitingFor_WhenTheBlockOutlastsTheEnrichWait_IsNone()
	{
		await Assert
			.That(EnricherChoice.WorthWaitingFor(MediaField.Tagline, [Candidate(RemoteService.Tmdb, MediaField.Tagline, Blocked(seconds: 30))], NoneWaited, Now))
			.IsNone();
	}

	[Test]
	public async Task WorthWaitingFor_WhenItHasNoEnrichWait_IsNoneEvenForAShortBlock()
	{
		var unbudgeted = Idle with { BlockedUntil = Some.Of(Now.AddSeconds(1)) };

		await Assert
			.That(EnricherChoice.WorthWaitingFor(MediaField.Tagline, [Candidate(RemoteService.Plex, MediaField.Tagline, unbudgeted)], NoneWaited, Now))
			.IsNone();
	}

	[Test]
	public async Task WorthWaitingFor_WhenItWasWaitedForAlready_IsNone()
	{
		await Assert
			.That(
				EnricherChoice.WorthWaitingFor(
					MediaField.Tagline,
					[Candidate(RemoteService.Tmdb, MediaField.Tagline, Blocked(seconds: 5))],
					new HashSet<RemoteService> { RemoteService.Tmdb },
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
			[Candidate(RemoteService.Tmdb, MediaField.Tagline, Blocked(seconds: 10)), Candidate(RemoteService.Plex, MediaField.Tagline, Blocked(seconds: 3))],
			NoneWaited,
			Now
		);

		await Assert.That(picked.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Plex);
	}

	[Test]
	public async Task NeedsRefetch_WhenNobodyLeftToAskCanFillAGap_IsFalse()
	{
		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [], Now)).IsFalse();
	}

	[Test]
	public async Task NeedsRefetch_WhenANewlyConnectedEnricherFillsAGap_IsTrue()
	{
		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [Candidate(RemoteService.Plex, PlexFills)], Now)).IsTrue();
	}

	[Test]
	public async Task NeedsRefetch_WhenTheOnlyEnricherThatFillsAGapIsBlocked_IsFalse()
	{
		var blocked = Candidate(RemoteService.Plex, PlexFills, Idle with { BlockedUntil = Some.Of(Now.AddMinutes(1)) });

		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [blocked], Now)).IsFalse();
	}

	[Test]
	public async Task NeedsRefetch_OnceTheBlockHasLifted_IsTrue()
	{
		var lifted = Candidate(RemoteService.Plex, PlexFills, Idle with { BlockedUntil = Some.Of(Now.AddMinutes(-1)) });

		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [lifted], Now)).IsTrue();
	}

	[Test]
	public async Task NeedsRefetch_WithinAnHourOfTheLastAsk_WaitsEvenThoughSomeoneNewCouldFillAGap()
	{
		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromMinutes(30)), [Candidate(RemoteService.Plex, PlexFills)], Now)).IsFalse();
	}

	[Test]
	public async Task NeedsRefetch_WhenNothingIsMissing_IsFalse()
	{
		var complete = Dune(askedAgo: TimeSpan.FromDays(1)) with { Details = Complete };

		await Assert.That(EnricherChoice.NeedsRefetch(complete, [Candidate(RemoteService.Plex, PlexFills)], Now)).IsFalse();
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
		var candidates = new[] { Candidate(RemoteService.Plex, MediaFields.AllRatings), Candidate(RemoteService.Tmdb, MediaFields.Rated([RatingSource.Tmdb])) };

		var next = EnricherChoice.Next(MediaFields.AllRatings, candidates, NoPreference, Now);

		await Assert.That(next.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Tmdb);
	}

	[Test]
	public async Task Next_WhenThePreferredProviderCanHelp_PicksItOverOneThatFillsMore()
	{
		var candidates = new[] { Candidate(RemoteService.Tmdb, MediaField.Tagline | MediaField.Summary), Candidate(RemoteService.Plex, MediaField.Tagline) };

		var next = EnricherChoice.Next(MediaField.Tagline | MediaField.Summary, candidates, new HashSet<RemoteService> { RemoteService.Plex }, Now);

		await Assert.That(next.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Plex);
	}

	[Test]
	public async Task Next_WhenThePreferredProviderIsBlocked_FallsThroughToTheOther()
	{
		var candidates = new[]
		{
			Candidate(RemoteService.Plex, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) }),
			Candidate(RemoteService.Tmdb, MediaField.Tagline),
		};

		var next = EnricherChoice.Next(MediaField.Tagline, candidates, new HashSet<RemoteService> { RemoteService.Plex }, Now);

		await Assert.That(next.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Tmdb);
	}

	[Test]
	public async Task Next_ForEqualFieldsAndLoad_PicksTmdbBeforePlex()
	{
		var candidates = new[] { Candidate(RemoteService.Plex, MediaField.Tagline), Candidate(RemoteService.Tmdb, MediaField.Tagline) };

		var next = EnricherChoice.Next(MediaField.Tagline, candidates, NoPreference, Now);

		await Assert.That(next.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Tmdb);
	}

	[Test]
	public async Task CanHelp_WhenItFillsNothingNeeded_IsFalse()
	{
		await Assert.That(Candidate(RemoteService.Tmdb, MediaField.Poster).CanHelp(MediaField.Tagline, Now)).IsFalse();
	}

	[Test]
	public async Task CanHelp_WhenItIsBlocked_IsFalse()
	{
		var blocked = Candidate(RemoteService.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) });

		await Assert.That(blocked.CanHelp(MediaField.Tagline, Now)).IsFalse();
	}

	[Test]
	public async Task CanHelp_WhenItIsOpenAndFillsANeededField_IsTrue()
	{
		await Assert.That(Candidate(RemoteService.Plex, MediaFields.AllRatings).CanHelp(MediaFields.Rated([RatingSource.Imdb]), Now)).IsTrue();
	}

	private static RemoteLoad Blocked(int seconds) => Idle with { BlockedUntil = Some.Of(Now.AddSeconds(seconds)), EnrichWait = TimeSpan.FromSeconds(15) };

	private static CachedDetails Dune(TimeSpan askedAgo)
	{
		var details = new MovieDetails { Title = "Dune", Ratings = MediaRatings.From([new TmdbRating(7.8f, Some.Of(12_000))]) };
		return new CachedDetails(details, Now.AddDays(-3), Now - askedAgo, [new ProviderAnswer(RemoteService.Tmdb, MediaField.Tagline, Now - askedAgo)]);
	}

	private static EnricherCandidate Candidate(RemoteService remote, MediaFields fills, RemoteLoad? load = null) =>
		new(remote, TmdbId.From("1", MediaType.Movie), fills, load ?? Idle);
}
