using Core.Media;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Media;

public class EnricherChoiceTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly RemoteLoad Idle = RemoteLoad.Idle;
	private static readonly IReadOnlySet<RemoteService> NoPreference = new HashSet<RemoteService>();
	private static readonly MediaFields PlexFills = MediaField.Tagline | MediaFields.Rated(RemoteService.Plex.SuppliedRatingSources());

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
	public async Task Next_ForEqualCoverage_PicksTheEnricherWithTheShorterWait() =>
		await Assert
			.That(
				EnricherChoice
					.Next(
						MediaField.Tagline,
						[
							Candidate(RemoteService.Tmdb, MediaField.Tagline, Idle with { EstimatedWait = TimeSpan.FromSeconds(8) }),
							Candidate(RemoteService.Mdblist, MediaField.Tagline, Idle with { EstimatedWait = TimeSpan.FromSeconds(1) }),
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
	public async Task Next_WhenTheBlockEndsWithinTheEnrichWait_AsksItAnyway()
	{
		var picked = EnricherChoice.Next(MediaField.Tagline, [Candidate(RemoteService.Tmdb, MediaField.Tagline, Blocked(seconds: 5))], NoPreference, Now);

		await Assert.That(picked.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Tmdb);
	}

	[Test]
	public async Task Next_WhenTheBlockOutlastsEnrichWait_SkipsTheEnricher()
	{
		await Assert
			.That(EnricherChoice.Next(MediaField.Tagline, [Candidate(RemoteService.Tmdb, MediaField.Tagline, Blocked(seconds: 30))], NoPreference, Now))
			.IsNone();
	}

	[Test]
	public async Task Next_WhenItHasNoEnrichWait_SkipsItEvenForAShortBlock()
	{
		var unbudgeted = Idle with { BlockedUntil = Some.Of(Now.AddSeconds(1)) };

		await Assert.That(EnricherChoice.Next(MediaField.Tagline, [Candidate(RemoteService.Plex, MediaField.Tagline, unbudgeted)], NoPreference, Now)).IsNone();
	}

	[Test]
	public async Task Next_WhenItIsDown_SkipsItWhateverTheEnrichWait()
	{
		var down = Idle with { Down = true, EnrichWait = TimeSpan.FromMinutes(1) };

		await Assert.That(EnricherChoice.Next(MediaField.Tagline, [Candidate(RemoteService.Tmdb, MediaField.Tagline, down)], NoPreference, Now)).IsNone();
	}

	[Test]
	public async Task Next_ForAPreferredEnricherBlockedWithinItsWait_PicksAnOpenOneFirst()
	{
		var candidates = new[] { Candidate(RemoteService.Plex, MediaField.Tagline, Blocked(seconds: 5)), Candidate(RemoteService.Tmdb, MediaField.Tagline) };

		var next = EnricherChoice.Next(MediaField.Tagline, candidates, new HashSet<RemoteService> { RemoteService.Plex }, Now);

		await Assert.That(next.Map(candidate => candidate.Remote)).IsSome().And.IsEqualTo(RemoteService.Tmdb);
	}

	[Test]
	public async Task Next_OfTwoBlockedWithinTheirWait_PicksTheSoonestFree()
	{
		var picked = EnricherChoice.Next(
			MediaField.Tagline,
			[Candidate(RemoteService.Tmdb, MediaField.Tagline, Blocked(seconds: 10)), Candidate(RemoteService.Plex, MediaField.Tagline, Blocked(seconds: 3))],
			NoPreference,
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
	public async Task NeedsRefetch_WhenTheEnricherThatFillsAGapIsBlockedWithinItsEnrichWait_IsTrue()
	{
		var blocked = Candidate(RemoteService.Plex, PlexFills, Blocked(seconds: 5));

		await Assert.That(EnricherChoice.NeedsRefetch(Dune(askedAgo: TimeSpan.FromDays(1)), [blocked], Now)).IsTrue();
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

	private static RemoteLoad Blocked(int seconds)
	{
		return Idle with { BlockedUntil = Some.Of(Now.AddSeconds(seconds)), EstimatedWait = TimeSpan.FromSeconds(seconds), EnrichWait = TimeSpan.FromSeconds(15) };
	}

	private static CachedDetails Dune(TimeSpan askedAgo)
	{
		var details = new MovieDetails { Title = "Dune", Ratings = MediaRatings.From([new TmdbRating(7.8f, Some.Of(12_000))]) };
		return new CachedDetails(details, Now.AddDays(-3), Now - askedAgo, [new ProviderAnswer(RemoteService.Tmdb, MediaField.Tagline, Now - askedAgo)]);
	}

	private static EnricherCandidate Candidate(RemoteService remote, MediaFields fills, RemoteLoad? load = null) =>
		new(remote, TmdbId.From("1", MediaType.Movie), fills, load ?? Idle);
}
