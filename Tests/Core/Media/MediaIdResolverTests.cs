using Contracts;
using Contracts.Caching;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Core.Media;
using Core.Tests.Feed;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Media;

public class MediaIdResolverTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly TmdbId Tmdb = TmdbId.From("1396", MediaType.Show);
	private static readonly PlexId Plex = PlexId.From("plex-1396", MediaType.Show);
	private static readonly TvdbId Tvdb = TvdbId.From("81189", MediaType.Show);
	private static readonly ImdbId Imdb = ImdbId.From("tt0903747", MediaType.Show);
	private static readonly IdentifyHints FromTmdb = new()
	{
		Title = "Breaking Bad",
		Year = 0,
		MediaType = MediaType.Show,
	};
	private static readonly AniListId AniList = AniListId.From("1535", MediaType.Show);
	private static readonly MalId Mal = MalId.From("1535", MediaType.Show);
	private static readonly ById ByShowId = new(
		Accepts: [MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb],
		Yields: [MediaProvider.AniList, MediaProvider.Mal],
		Cost: 0,
		GivesHints: false
	);
	private static readonly ById ByAnimeId = new(Accepts: [MediaProvider.AniList, MediaProvider.Mal], Yields: [MediaProvider.Tvdb], Cost: 0, GivesHints: false);
	private static readonly ById Detail = new(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);
	private static readonly ById PlexById = new(Accepts: [MediaProvider.Plex], Yields: [MediaProvider.Tvdb], Cost: 1);
	private static readonly ByHints Search = new(Yields: [MediaProvider.Plex, MediaProvider.Tvdb], Cost: 4);
	private static readonly IdentifyRequest Request = new([Tmdb], new None());
	private static readonly CancellationToken WaitingThrows = new(canceled: true);
	private static readonly IReadOnlySet<RemoteService> Everyone = Enum.GetValues<RemoteService>().ToHashSet();

	private readonly Dictionary<Mock<IExternalIdConverter>, RemoteService> _remotes = [];
	private readonly Mock<IExternalIdConverter> _tmdb;
	private readonly Mock<IExternalIdConverter> _plex;
	private readonly Mock<ITypedMemoryCache<string, bool>> _cache = TestUnmatchedCache.Empty();

	public MediaIdResolverTests()
	{
		_tmdb = Converter(RemoteService.Tmdb, Detail);
		_plex = Converter(RemoteService.Plex, PlexById, Search);
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderIsBlockedWithinItsMaxWait_WaitsThenTakesThePath()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee(TmdbBlocked(() => Now.AddMilliseconds(20))).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhileWaitingForABlockedProvider_HonoursCancellation()
	{
		var testee = Testee(TmdbBlocked(() => Now.AddMilliseconds(20)));

		await Assert.That(async () => await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone, WaitingThrows)).Throws<OperationCanceledException>();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenABlockOutlastsTheMaxWait_GivesUpWithoutCachingTheTitle()
	{
		var resolved = await Testee(TmdbBlocked(() => Now.AddMinutes(1))).ResolveAsync(Request, MediaProvider.Tvdb, Everyone, WaitingThrows);

		await Assert.That(resolved).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<LookupNotRunException>()).IsSome());
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenTheBlockIsExtendedDuringTheWait_GivesUpWithoutCachingTheTitle()
	{
		var pushes = 0;

		var resolved = await Testee(TmdbBlocked(() => Now.AddMilliseconds(20 + pushes++))).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderIsBlockedAgainAfterTheWait_DoesNotWaitTwice()
	{
		var tmdbFreeAt = Now.AddMilliseconds(20);
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					tmdbFreeAt = Now.AddMilliseconds(40);
					return HintsOnly();
				}
			);

		var resolved = await Testee(TmdbBlocked(() => tmdbFreeAt)).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderThatWentDownIsBlocked_NeverWaitsForIt()
	{
		var tmdbFreeAt = Now;
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					tmdbFreeAt = Now.AddSeconds(5);
					return Error.From(new RemoteUnavailableException(RemoteService.Tmdb));
				}
			);

		var resolved = await Testee(TmdbBlocked(() => tmdbFreeAt)).ResolveAsync(Request, MediaProvider.Tvdb, Everyone, WaitingThrows);

		await Assert.That(resolved).IsError();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_AfterASuccessfulWait_CachesAnAnsweredMissAsUnmatched()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Error.From("TMDB knows no TVDB id."));

		var resolved = await Testee(TmdbBlocked(() => Now.AddMilliseconds(20)), _tmdb).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForATitleHoldingTheTarget_GainsTheIdsAFreeLookupAnswers()
	{
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		Answer(map, new ByIdRequest(ByShowId, Tvdb), Found(AniList, Mal));

		var resolved = await Testee(map, _tmdb).ResolveAsync(new IdentifyRequest([Tvdb], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).IsEquivalentTo(new IExternalMediaId[] { Tvdb, AniList, Mal }));
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_ForATitleHoldingSeveralIdsAFreeLookupAccepts_AsksItOnce()
	{
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		map.LookUpAsync(Any(), Any()).Returns(Found(AniList, Mal));

		await Testee(map).ResolveAsync(new IdentifyRequest([Tvdb, Imdb, Tmdb], new None()), MediaProvider.Tvdb, Everyone);

		map.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForAProviderTheTitleAlreadyHolds_AddsNoSecondIdOfIt()
	{
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		var other = AniListId.From("99999", MediaType.Show);
		Answer(map, new ByIdRequest(ByShowId, Tvdb), Found(other, Mal));

		var resolved = await Testee(map).ResolveAsync(new IdentifyRequest([Tvdb, AniList], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).IsEquivalentTo(new IExternalMediaId[] { Tvdb, AniList, Mal }));
	}

	[Test]
	public async Task ResolveAsync_WhileAFreeLookupThatYieldsTheTargetIsDown_DoesNotCacheTheTitleAsUnmatched()
	{
		var map = Converter(RemoteService.AnimeLists, ByAnimeId);
		map.LookUpAsync(Any(), Any()).Returns(Error.From(new RemoteUnavailableException(RemoteService.AnimeLists)));

		var resolved = await Testee(map).ResolveAsync(new IdentifyRequest([AniList], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhileOnlyAFreeLookupThatYieldsOtherIdsIsDown_CachesTheTitleAsUnmatched()
	{
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		map.LookUpAsync(Any(), Any()).Returns(Error.From(new RemoteUnavailableException(RemoteService.AnimeLists)));

		var resolved = await Testee(map).ResolveAsync(new IdentifyRequest([Imdb], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForTheCheapestOpenPath_ResolvesWithoutTryingTheOthers()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).IsEquivalentTo(new IExternalMediaId[] { Tmdb, Tvdb }));
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenTheCheapestPathFindsNothing_FollowsTheNextPathHopByHop()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex
			.LookUpAsync(Any(), Any())
			.Returns(
				(request, _) => request is ByHintsRequest { Known: var known } && known.Contains(Tmdb) ? Found(Plex, Tvdb) : Error.From("The TMDB id never arrived.")
			);

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhileTmdbIsNotConnected_AsksNothingOfIt()
	{
		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, new HashSet<RemoteService> { RemoteService.Plex });

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_HandsTheHintsTmdbFoundToTheFollowingSearch()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex
			.LookUpAsync(Any(), Any())
			.Returns((request, _) => request is ByHintsRequest { Hints: var given } && given == FromTmdb ? Found(Plex) : Error.From("TMDB's hints never arrived."));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Plex, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Plex));
	}

	[Test]
	public async Task ResolveAsync_WhenTwoHopsAskTheSameLookupForTheSameId_CallsItOnce()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex.LookUpAsync(Any(), Any()).Returns(Error.From("Plex found nothing."));

		await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_NeverAsksALookupByAnIdItsOwnAnswerGave()
	{
		var anyId = new ById(Accepts: [MediaProvider.Imdb, MediaProvider.Trakt], Yields: [MediaProvider.Imdb, MediaProvider.Trakt, MediaProvider.Tvdb], Cost: 2);
		var mdblist = Converter(RemoteService.Mdblist, anyId);
		Answer(mdblist, new ByIdRequest(anyId, Imdb), Found(Imdb, TraktId.From("1388", MediaType.Show)));

		var resolved = await Testee(mdblist).ResolveAsync(new IdentifyRequest([Imdb], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		mdblist.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForTwoLookupsOfEqualCost_KeepsTheirAnswersApart()
	{
		var first = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Tvdb], Cost: 1);
		var second = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);
		var tmdb = Converter(RemoteService.Tmdb, first, second);
		Answer(tmdb, new ByIdRequest(first, Tmdb), Error.From("The first lookup knows no TVDB id."));
		Answer(tmdb, new ByIdRequest(second, Tmdb), Found(Tvdb));

		var resolved = await Testee(tmdb).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_ForEqualLookupsOfTwoConverters_KeepsTheirAnswersApart()
	{
		var other = Converter(RemoteService.Wikidata, Detail);
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Error.From("TMDB knows no TVDB id."));
		Answer(other, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee(_tmdb, other).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
		other.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForASearchWithOtherKnownIds_AsksPlexAgain()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Success.Of(new IdLookupResult([Imdb], Some.Of(FromTmdb))));
		_plex.LookUpAsync(Any(), Any()).Returns(Error.From("Plex found nothing."));

		await Testee().ResolveAsync(new IdentifyRequest([Tmdb], Some.Of(FromTmdb)), MediaProvider.Tvdb, Everyone);

		_plex.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_WhenTheFirstPlexIdLacksTheWantedId_TriesTheSecond()
	{
		var second = PlexId.From("plex-81189", MediaType.Show);
		Answer(_plex, new ByIdRequest(PlexById, Plex), Found(Plex));
		Answer(_plex, new ByIdRequest(PlexById, second), Found(second, Tvdb));

		var resolved = await Testee().ResolveAsync(new IdentifyRequest([Plex, second], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhenTmdbKnowsOnlyTheSecondImdbId_FindsTheTmdbIdThroughIt()
	{
		var find = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tmdb], Cost: 1);
		var tmdb = Converter(RemoteService.Tmdb, find);
		var unknown = ImdbId.From("tt0000001", MediaType.Show);
		Answer(tmdb, new ByIdRequest(find, unknown), Error.From("TMDB has no match for tt0000001."));
		Answer(tmdb, new ByIdRequest(find, Imdb), Found(Tmdb));

		var resolved = await Testee(tmdb).ResolveAsync(new IdentifyRequest([unknown, Imdb], new None()), MediaProvider.Tmdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tmdb));
	}

	[Test]
	public async Task ResolveAsync_WhenAnAnswerLacksTheWantedId_FailsTheHopAndRunsTheNextPath()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Imdb));
		Answer(_plex, new ByIdRequest(PlexById, Plex), Found(Tvdb));

		var resolved = await Testee().ResolveAsync(new IdentifyRequest([Tmdb, Plex], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhenTheCheapestPathIsRateLimited_FallsThroughToAPathAroundIt()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Error.From(new RemoteUnavailableException(RemoteService.Tmdb)));
		Answer(_plex, new ByIdRequest(PlexById, Plex), Found(Tvdb));

		var resolved = await Testee().ResolveAsync(new IdentifyRequest([Tmdb, Plex], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderIsUnavailable_SkipsTheRemainingPathsThroughItAndCachesNothing()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Error.From(new RemoteUnavailableException(RemoteService.Tmdb)));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<LookupNotRunException>()).IsSome());
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderGetsBlockedWhileResolving_SkipsTheRemainingPathsThroughIt()
	{
		var tmdbBlocked = false;
		var capacity = TestCapacity.With(remote => new RemoteLoad(Optional.When(tmdbBlocked && remote is RemoteService.Tmdb, () => Now.AddMinutes(1)), 0));
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					tmdbBlocked = true;
					return HintsOnly();
				}
			);

		var resolved = await Testee(capacity).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderGetsBlockedMidPath_StopsBeforeTheHopThroughIt()
	{
		var plexBlocked = false;
		var capacity = TestCapacity.With(remote => new RemoteLoad(Optional.When(plexBlocked && remote is RemoteService.Plex, () => Now.AddMinutes(1)), 0));
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					plexBlocked = true;
					return HintsOnly();
				}
			);

		var resolved = await Testee(capacity).ResolveAsync(Request, MediaProvider.Plex, Everyone);

		await Assert.That(resolved).IsError();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenEveryPathFindsNothing_CachesTheTitleAsUnmatched()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex.LookUpAsync(Any(), Any()).Returns(Error.From("Plex found nothing."));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<LookupNotRunException>()).IsNone());
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenAConverterThrows_TreatsItAsTransient()
	{
		_tmdb.LookUpAsync(Any(), Any()).Throws(new InvalidOperationException("transient"));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<LookupNotRunException>()).IsSome());
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenEveryPathRunsThroughABlockedProvider_AsksNobodyAndCachesNothing()
	{
		var blocked = TestCapacity.With(_ => new RemoteLoad(Some.Of(Now.AddMinutes(1)), 0));

		var resolved = await Testee(blocked).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_ForATitleThatAlreadyHoldsTheTarget_ReturnsItsCandidatesWithoutCachingItAsUnmatched()
	{
		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tmdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).IsEquivalentTo(new IExternalMediaId[] { Tmdb }));
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenNoLookupReachesTheTarget_CachesTheTitleAsUnmatched()
	{
		var resolved = await Testee().ResolveAsync(new IdentifyRequest([MdblistId.From("42", MediaType.Show)], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheTitleIsCachedAsUnmatched_AsksNobody()
	{
		_cache.GetValueOrNone(Any()).Returns(Some.Of(true));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_ThroughABlockedPath_RecordsGaveUp()
	{
		using var trace = new TitleTrace();

		var resolved = await Testee(TmdbBlocked(() => Now.AddMinutes(1))).ResolveAsync(Request, MediaProvider.Tvdb, Everyone, WaitingThrows);

		await Assert.That(resolved).IsError();
		await Assert.That((await trace.NodeAsync(RemoteService.Tmdb, TraceStage.Ids)).GaveUp).IsEqualTo(1);
	}

	[Test]
	public async Task ResolveAsync_WhenStillBlockedAfterTheWait_RecordsGaveUp()
	{
		using var trace = new TitleTrace();
		var pushes = 0;

		await Testee(TmdbBlocked(() => Now.AddMilliseconds(20 + pushes++))).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That((await trace.NodeAsync(RemoteService.Tmdb, TraceStage.Ids)).GaveUp).IsEqualTo(1);
	}

	[Test]
	public async Task ResolveAsync_ForAProviderThatWentDown_RecordsNoGaveUp()
	{
		using var trace = new TitleTrace();
		var tmdbFreeAt = Now;
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					tmdbFreeAt = Now.AddSeconds(5);
					return Error.From(new RemoteUnavailableException(RemoteService.Tmdb));
				}
			);

		await Testee(TmdbBlocked(() => tmdbFreeAt)).ResolveAsync(Request, MediaProvider.Tvdb, Everyone, WaitingThrows);

		await Assert.That((await trace.NodesAsync()).Sum(node => node.GaveUp)).IsEqualTo(0);
	}

	[Test]
	public async Task ResolveAsync_WhenAFreeHopFindsNothing_RecordsNoHop()
	{
		using var trace = new TitleTrace();
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		Answer(map, new ByIdRequest(ByShowId, Tvdb), Found());

		await Testee(map).ResolveAsync(new IdentifyRequest([Tvdb], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(await trace.NodesAsync()).IsEmpty();
	}

	[Test]
	public async Task ResolveAsync_WhenAFreeHopAddsIds_RecordsItAnswered()
	{
		using var trace = new TitleTrace();
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		Answer(map, new ByIdRequest(ByShowId, Tvdb), Found(AniList, Mal));

		var resolved = await Testee(map).ResolveAsync(new IdentifyRequest([Tvdb], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).IsEquivalentTo(new IExternalMediaId[] { Tvdb, AniList, Mal }));
		await Assert.That((await trace.NodeAsync(RemoteService.AnimeLists, TraceStage.Ids)).Answered).IsEqualTo(1);
	}

	[Test]
	public async Task ResolveAsync_WithAnAnswerAnEarlierPathGot_RecordsOneHop()
	{
		using var trace = new TitleTrace();
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex.LookUpAsync(Any(), Any()).Returns(Error.From("Plex found nothing."));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
		await Assert.That((await trace.NodeAsync(RemoteService.Tmdb, TraceStage.Ids)).Answered).IsEqualTo(1);
	}

	private MediaIdResolver Testee(IRemoteCapacity? capacity = null) => Testee(capacity ?? TestCapacity.Open(), _tmdb, _plex);

	private MediaIdResolver Testee(params Mock<IExternalIdConverter>[] converters) => Testee(TestCapacity.Open(), converters);

	private MediaIdResolver Testee(IRemoteCapacity capacity, params Mock<IExternalIdConverter>[] converters)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(Now);
		return new MediaIdResolver(
			[.. converters.Select(converter => new Keyed<RemoteService, IExternalIdConverter>(_remotes[converter], converter.Object))],
			_cache.Object,
			capacity,
			IServiceReadiness.Mock().Object,
			clock.Object
		);
	}

	private static IRemoteCapacity TmdbBlocked(Func<DateTimeOffset> until)
	{
		var maxWait = TimeSpan.FromSeconds(15);
		return TestCapacity.With(remote => remote is RemoteService.Tmdb ? new RemoteLoad(Some.Of(until()), 0, maxWait) : new RemoteLoad(new None(), 0));
	}

	private Mock<IExternalIdConverter> Converter(RemoteService remote, params IdLookup[] lookups)
	{
		var converter = IExternalIdConverter.Mock();
		converter.Lookups.Returns(lookups);
		_remotes[converter] = remote;
		return converter;
	}

	private static void Answer(Mock<IExternalIdConverter> converter, IdLookupRequest request, Result<IdLookupResult> answer)
	{
		converter.LookUpAsync(request, Any()).Returns(answer);
	}

	private static Result<IdLookupResult> Found(params IExternalMediaId[] ids) => Success.Of(new IdLookupResult(ids, new None()));

	private static Result<IdLookupResult> HintsOnly() => Success.Of(new IdLookupResult([], Some.Of(FromTmdb)));
}
