using System.Text.Json;
using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Core.Caching;
using Core.Media;
using Core.Tests.Feed;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TestInfrastructure;

namespace Core.Tests.Media;

[NotInParallel("typed-memory-cache-clear")]
public sealed class MediaIdResolverTests : IDisposable
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
	private static readonly ById ByGuid = new(
		Accepts: [MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb],
		Yields: [MediaProvider.Plex],
		Cost: 1,
		GivesHints: false,
		AsksOnce: true
	);
	private static readonly IdentifyRequest Request = new([Tmdb], new None());
	private static readonly IReadOnlySet<RemoteService> Everyone = Enum.GetValues<RemoteService>().ToHashSet();

	private readonly Dictionary<Mock<IExternalIdConverter>, RemoteService> _remotes = [];
	private readonly Mock<IExternalIdConverter> _tmdb;
	private readonly Mock<IExternalIdConverter> _plex;
	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly TypedMemoryCache<string, bool> _cache;

	private readonly Mock<ILogger<MediaIdResolver>> _logger = ILogger<MediaIdResolver>.Mock();

	public MediaIdResolverTests()
	{
		_tmdb = Converter(RemoteService.Tmdb, Detail);
		_plex = Converter(RemoteService.Plex, PlexById, Search);
		_cache = new TypedMemoryCache<string, bool>(_memory);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task ResolveAsync_WhenAConverterFaults_WarnsWithItsException()
	{
		var broken = new JsonException("The JSON value could not be converted.");
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), new Faulted(RemoteService.Tmdb, Error.From(broken)));

		await Testee(TestCapacity.Open(), _tmdb).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		var warning = Mock.Invocations(_logger).Single(call => call.Arguments[0] is LogLevel.Warning);
		await Assert.That($"{warning.Arguments[2]}").IsEqualTo("Tmdb could not answer an id lookup");
		await Assert.That(warning.Arguments[3]).IsSameReferenceAs(broken);
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderIsBlockedWithinItsMaxWait_TakesThePathAndLeavesTheWaitToItsLane()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee(TmdbBlocked(() => Now.AddMilliseconds(20))).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhenTheOnlyRouteIsBlockedPastMaxWait_GivesUpAsCouldNotLookUpWithoutCaching()
	{
		using var trace = new TitleTrace();
		var blockedUntil = Now.AddMinutes(1);
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Tvdb));
		var testee = Testee(TmdbBlocked(() => blockedUntil));

		var resolved = await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);
		blockedUntil = Now;
		var again = await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotRun>();
		await Assert.That((await trace.NodeAsync(RemoteService.Tmdb, TraceStage.Ids)).GaveUp).IsEqualTo(1);
		await Assert.That(again).IsOfUnionType<Resolution, Resolved>();
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderWentDown_TakesNoOtherPathThroughIt()
	{
		var find = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tvdb], Cost: 2);
		var tmdb = Converter(RemoteService.Tmdb, Detail, find);
		Answer(tmdb, new ByIdRequest(Detail, Tmdb), Down(RemoteService.Tmdb));

		var resolved = await Testee(TestCapacity.Open(), tmdb, _plex).ResolveAsync(new IdentifyRequest([Tmdb, Imdb], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotRun>();
		tmdb.LookUpAsync((IdLookupRequest)new ByIdRequest(find, Imdb), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderBlockedWithinItsMaxWaitAnswersAMiss_CachesTheTitleAsUnmatched()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), new NoMatch(RemoteService.Tmdb));
		var testee = Testee(TmdbBlocked(() => Now.AddMilliseconds(20)), _tmdb);

		var resolved = await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);
		var again = await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NoMatchAnywhere>();
		await Assert.That(again).IsOfUnionType<Resolution, NotFoundEarlier>();
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheFirstHopMissesAndThePathsBehindItCrossABlockedProvider_SettlesNoMatchAndCachesIt()
	{
		var (mdblist, byMal, trakt) = AnimePaths();
		Answer(mdblist, new ByIdRequest(byMal, Mal), new NoMatch(RemoteService.Mdblist));
		var request = new IdentifyRequest([Mal], new None());
		var testee = Testee(TraktBlocked(), mdblist, trakt);

		var resolved = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);
		var again = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NoMatchAnywhere>();
		await Assert.That(again).IsOfUnionType<Resolution, NotFoundEarlier>();
		mdblist.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
		trakt.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenTheFirstHopFindsAnImdbIdAndTheOnlyHopOnIsBlocked_IsNotRun()
	{
		var (mdblist, byMal, trakt) = AnimePaths();
		Answer(mdblist, new ByIdRequest(byMal, Mal), Found(Mal, Imdb));
		var request = new IdentifyRequest([Mal], new None());
		var testee = Testee(TraktBlocked(), mdblist, trakt);

		var resolved = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);
		await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotRun>();
		mdblist.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
		trakt.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_ForATitleHoldingTheTarget_GainsTheIdsAFreeLookupAnswersAndRecordsItAnswered()
	{
		using var trace = new TitleTrace();
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		Answer(map, new ByIdRequest(ByShowId, Tvdb), Found(AniList, Mal));

		var resolved = await Testee(map, _tmdb).ResolveAsync(new IdentifyRequest([Tvdb], new None()), MediaProvider.Tvdb, Everyone);

		await Assert
			.That(resolved)
			.IsOfUnionType<Resolution, Resolved>()
			.And.Multiple(async found => await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { Tvdb, AniList, Mal }));
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
		await Assert.That((await trace.NodeAsync(RemoteService.AnimeLists, TraceStage.Ids)).Answered).IsEqualTo(1);
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

		await Assert
			.That(resolved)
			.IsOfUnionType<Resolution, Resolved>()
			.And.Multiple(async found => await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { Tvdb, AniList, Mal }));
	}

	[Test]
	public async Task ResolveAsync_WhileAFreeLookupThatYieldsTheTargetIsDown_DoesNotCacheTheTitleAsUnmatched()
	{
		var map = Converter(RemoteService.AnimeLists, ByAnimeId);
		map.LookUpAsync(Any(), Any()).Returns(Down(RemoteService.AnimeLists));
		var request = new IdentifyRequest([AniList], new None());
		var testee = Testee(map);

		var resolved = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);
		await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotRun>();
		map.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_WhileOnlyAFreeLookupThatYieldsOtherIdsIsDown_CachesTheTitleAsUnmatched()
	{
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		map.LookUpAsync(Any(), Any()).Returns(Down(RemoteService.AnimeLists));
		var request = new IdentifyRequest([Imdb], new None());
		var testee = Testee(map);

		var resolved = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);
		var again = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NoMatchAnywhere>();
		await Assert.That(again).IsOfUnionType<Resolution, NotFoundEarlier>();
		map.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForTheCheapestOpenPath_ResolvesWithoutTryingTheOthers()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert
			.That(resolved)
			.IsOfUnionType<Resolution, Resolved>()
			.And.Multiple(async found => await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { Tmdb, Tvdb }));
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenTheCheapestPathFindsNothing_FollowsTheNextPathHopByHop()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex
			.LookUpAsync(Any(), Any())
			.Returns((request, _) => request is ByHintsRequest { Known: var known } && known.Contains(Tmdb) ? Found(Plex, Tvdb) : new NoMatch(RemoteService.Plex));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhileTmdbIsNotConnected_AsksNothingOfIt()
	{
		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, new HashSet<RemoteService> { RemoteService.Plex });

		await Assert.That(resolved).IsOfUnionType<Resolution, NoMatchAnywhere>();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_HandsTheHintsTmdbFoundToTheFollowingSearch()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex
			.LookUpAsync(Any(), Any())
			.Returns((request, _) => request is ByHintsRequest { Hints: var given } && given == FromTmdb ? Found(Plex) : new NoMatch(RemoteService.Plex));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Plex, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Plex));
	}

	[Test]
	public async Task ResolveAsync_WhenTheMatchMissesByTmdb_DoesNotAskByImdb()
	{
		var plex = Converter(RemoteService.Plex, ByGuid);
		plex.LookUpAsync(Any(), Any()).Returns(Found());

		await Testee(plex).ResolveAsync(new IdentifyRequest([Tmdb, Imdb], new None()), MediaProvider.Plex, Everyone);

		plex.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheMatchMisses_AsksNothingAlongThePathsThatEndInIt()
	{
		var find = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tmdb], Cost: 1);
		var tmdb = Converter(RemoteService.Tmdb, find);
		var plex = Converter(RemoteService.Plex, ByGuid);
		plex.LookUpAsync(Any(), Any()).Returns(Found());

		await Testee(tmdb, plex).ResolveAsync(new IdentifyRequest([Imdb], new None()), MediaProvider.Plex, Everyone);

		tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Never);
		plex.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheMatchMissesByOneTmdbId_DoesNotAskByTheOther()
	{
		var plex = Converter(RemoteService.Plex, ByGuid);
		plex.LookUpAsync(Any(), Any()).Returns(Found());

		await Testee(plex).ResolveAsync(new IdentifyRequest([Tmdb, TmdbId.From("1397", MediaType.Show)], new None()), MediaProvider.Plex, Everyone);

		plex.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheMatchFindsTheTitleButALaterHopFails_StillTakesAnotherPathThroughTheMatch()
	{
		var byPlex = new ById(Accepts: [MediaProvider.Plex], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1, GivesHints: false);
		var toTvdb = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tvdb], Cost: 2, GivesHints: false);
		var plex = Converter(RemoteService.Plex, ByGuid, byPlex);
		var tvmaze = Converter(RemoteService.Tvmaze, toTvdb);
		Answer(plex, new ByIdRequest(ByGuid, Tmdb), Found(Plex, Tmdb));
		Answer(plex, new ByIdRequest(byPlex, Plex), Found(Plex, Imdb));
		Answer(tvmaze, new ByIdRequest(toTvdb, Imdb), Found(Imdb, Tvdb));

		var resolved = await Testee(plex, tvmaze).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>();
	}

	[Test]
	public async Task ResolveAsync_WhenTheMatchFailsByTmdb_StillAsksByImdb()
	{
		var plex = Converter(RemoteService.Plex, ByGuid);
		Answer(plex, new ByIdRequest(ByGuid, Tmdb), new Faulted(RemoteService.Plex, Error.From(new InvalidOperationException("unreadable"))));
		Answer(plex, new ByIdRequest(ByGuid, Imdb), Found(Plex, Imdb));

		var resolved = await Testee(plex).ResolveAsync(new IdentifyRequest([Tmdb, Imdb], new None()), MediaProvider.Plex, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>();
		plex.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_NeverAsksALookupByAnIdItsOwnAnswerGave()
	{
		var anyId = new ById(Accepts: [MediaProvider.Imdb, MediaProvider.Trakt], Yields: [MediaProvider.Imdb, MediaProvider.Trakt, MediaProvider.Tvdb], Cost: 2);
		var mdblist = Converter(RemoteService.Mdblist, anyId);
		Answer(mdblist, new ByIdRequest(anyId, Imdb), Found(Imdb, TraktId.From("1388", MediaType.Show)));

		var resolved = await Testee(mdblist).ResolveAsync(new IdentifyRequest([Imdb], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NoMatchAnywhere>();
		mdblist.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForTwoLookupsOfEqualCost_KeepsTheirAnswersApart()
	{
		var first = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Tvdb], Cost: 1);
		var second = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);
		var tmdb = Converter(RemoteService.Tmdb, first, second);
		Answer(tmdb, new ByIdRequest(first, Tmdb), new NoMatch(RemoteService.Tmdb));
		Answer(tmdb, new ByIdRequest(second, Tmdb), Found(Tvdb));

		var resolved = await Testee(tmdb).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_ForEqualLookupsOfTwoConverters_KeepsTheirAnswersApart()
	{
		var other = Converter(RemoteService.Wikidata, Detail);
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), new NoMatch(RemoteService.Tmdb));
		Answer(other, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee(_tmdb, other).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Tvdb));
		other.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForASearchWithOtherKnownIds_AsksPlexAgain()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), new Found<IdLookupResult>(new IdLookupResult([Imdb], Some.Of(FromTmdb))));
		_plex.LookUpAsync(Any(), Any()).Returns(new NoMatch(RemoteService.Plex));

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

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhenTmdbKnowsOnlyTheSecondImdbId_FindsTheTmdbIdThroughIt()
	{
		var find = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tmdb], Cost: 1);
		var tmdb = Converter(RemoteService.Tmdb, find);
		var unknown = ImdbId.From("tt0000001", MediaType.Show);
		Answer(tmdb, new ByIdRequest(find, unknown), new NoMatch(RemoteService.Tmdb));
		Answer(tmdb, new ByIdRequest(find, Imdb), Found(Tmdb));

		var resolved = await Testee(tmdb).ResolveAsync(new IdentifyRequest([unknown, Imdb], new None()), MediaProvider.Tmdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Tmdb));
	}

	[Test]
	public async Task ResolveAsync_WhenAnAnswerLacksTheWantedId_FailsTheHopAndRunsTheNextPath()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Imdb));
		Answer(_plex, new ByIdRequest(PlexById, Plex), Found(Tvdb));

		var resolved = await Testee().ResolveAsync(new IdentifyRequest([Tmdb, Plex], new None()), MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhenTheCheapestPathIsRateLimited_FallsThroughToAPathAroundIt()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Down(RemoteService.Tmdb));
		Answer(_plex, new ByIdRequest(PlexById, Plex), Found(Tvdb));
		var request = new IdentifyRequest([Tmdb, Plex], new None());
		var testee = Testee();

		var resolved = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);
		await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, Resolved>().And.Multiple(async found => await Assert.That(found.Ids).Contains(Tvdb));
		_plex.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_WhenAnyRouteIsUnavailable_IsNotRunAndCachesNothing()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Down(RemoteService.Tmdb));
		Answer(_plex, new ByIdRequest(PlexById, Plex), new NoMatch(RemoteService.Plex));
		var request = new IdentifyRequest([Tmdb, Plex], new None());
		var testee = Testee();

		var resolved = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);
		await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotRun>();
		_plex.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_ForAResolvedTitle_NamesTheConvertersThatAnswered()
	{
		var map = Converter(RemoteService.AnimeLists, ByShowId);
		Answer(map, new ByIdRequest(ByShowId, Tmdb), Found(AniList));
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee(map, _tmdb).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert
			.That(resolved)
			.IsOfUnionType<Resolution, Resolved>()
			.And.Multiple(async found => await Assert.That(found.Sources).IsEquivalentTo([RemoteService.AnimeLists, RemoteService.Tmdb]));
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderGetsBlockedWhileResolving_SkipsTheRemainingPathsThroughIt()
	{
		var tmdbBlocked = false;
		var capacity = TestCapacity.With(remote =>
			RemoteLoad.Idle with
			{
				BlockedUntil = Optional.When(tmdbBlocked && remote is RemoteService.Tmdb, () => Now.AddMinutes(1)),
			}
		);
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					tmdbBlocked = true;
					return HintsOnly();
				}
			);

		var testee = Testee(capacity);

		var resolved = await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);
		tmdbBlocked = false;
		await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotRun>();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderGetsBlockedMidPath_StopsBeforeTheHopThroughIt()
	{
		var plexBlocked = false;
		var capacity = TestCapacity.With(remote =>
			RemoteLoad.Idle with
			{
				BlockedUntil = Optional.When(plexBlocked && remote is RemoteService.Plex, () => Now.AddMinutes(1)),
			}
		);
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					plexBlocked = true;
					return HintsOnly();
				}
			);

		var testee = Testee(capacity);

		var resolved = await testee.ResolveAsync(Request, MediaProvider.Plex, Everyone);
		plexBlocked = false;
		await testee.ResolveAsync(Request, MediaProvider.Plex, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotRun>();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_WhenEveryPathFindsNothing_CachesTheTitleAsUnmatched()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex.LookUpAsync(Any(), Any()).Returns(new NoMatch(RemoteService.Plex));
		var testee = Testee();

		var resolved = await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);
		var again = await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NoMatchAnywhere>();
		await Assert.That(again).IsOfUnionType<Resolution, NotFoundEarlier>();
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenAConverterFaults_TreatsItAsTransient()
	{
		_tmdb.LookUpAsync(Any(), Any()).Returns(new Faulted(RemoteService.Tmdb, Error.From(new InvalidOperationException("unreadable"))));
		var testee = Testee();

		var resolved = await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);
		await testee.ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotRun>();
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_ForATitleThatAlreadyHoldsTheTarget_ReturnsItsCandidatesWithoutCachingItAsUnmatched()
	{
		var testee = Testee();

		var resolved = await testee.ResolveAsync(Request, MediaProvider.Tmdb, Everyone);
		var again = await testee.ResolveAsync(Request, MediaProvider.Tmdb, Everyone);

		await Assert
			.That(resolved)
			.IsOfUnionType<Resolution, Resolved>()
			.And.Multiple(async found => await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { Tmdb }));
		await Assert.That(again).IsOfUnionType<Resolution, Resolved>();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenNoLookupReachesTheTarget_CachesTheTitleAsUnmatched()
	{
		var request = new IdentifyRequest([MdblistId.From("42", MediaType.Show)], new None());
		var testee = Testee();

		var resolved = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);
		var again = await testee.ResolveAsync(request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NoMatchAnywhere>();
		await Assert.That(again).IsOfUnionType<Resolution, NotFoundEarlier>();
	}

	[Test]
	public async Task ResolveAsync_WhenTheTitleIsCachedAsUnmatched_AsksNobody()
	{
		_cache.Set(UnmatchedCacheKey.Build(Request.Candidates, MediaProvider.Tvdb, Everyone), true);

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NotFoundEarlier>();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
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
					return Down(RemoteService.Tmdb);
				}
			);

		await Testee(TmdbBlocked(() => tmdbFreeAt)).ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

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
	public async Task ResolveAsync_WithAnAnswerAnEarlierPathGot_RecordsOneHop()
	{
		using var trace = new TitleTrace();
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex.LookUpAsync(Any(), Any()).Returns(new NoMatch(RemoteService.Plex));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb, Everyone);

		await Assert.That(resolved).IsOfUnionType<Resolution, NoMatchAnywhere>();
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
			_cache,
			capacity,
			IServiceReadiness.Mock().Object,
			clock.Object,
			_logger.Object
		);
	}

	private static IRemoteCapacity TmdbBlocked(Func<DateTimeOffset> until)
	{
		return TestCapacity.With(remote =>
			remote is RemoteService.Tmdb ? RemoteLoad.Idle with { BlockedUntil = Some.Of(until()), MaxWait = TimeSpan.FromSeconds(15) } : RemoteLoad.Idle
		);
	}

	private static IRemoteCapacity TraktBlocked()
	{
		return TestCapacity.With(remote => remote is RemoteService.Trakt ? RemoteLoad.Idle with { BlockedUntil = Some.Of(Now.AddMinutes(1)) } : RemoteLoad.Idle);
	}

	private (Mock<IExternalIdConverter> Mdblist, ById ByMal, Mock<IExternalIdConverter> Trakt) AnimePaths()
	{
		var byMal = new ById(Accepts: [MediaProvider.Mal], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1, GivesHints: false);
		var toTvdb = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tvdb], Cost: 1, GivesHints: false);
		return (Converter(RemoteService.Mdblist, byMal), byMal, Converter(RemoteService.Trakt, toTvdb));
	}

	private Mock<IExternalIdConverter> Converter(RemoteService remote, params IdLookup[] lookups)
	{
		var converter = IExternalIdConverter.Mock();
		converter.Lookups.Returns(lookups);
		_remotes[converter] = remote;
		return converter;
	}

	private static void Answer(Mock<IExternalIdConverter> converter, IdLookupRequest request, Answer<IdLookupResult> answer)
	{
		converter.LookUpAsync(request, Any()).Returns(answer);
	}

	private static Answer<IdLookupResult> Found(params IExternalMediaId[] ids) => new Found<IdLookupResult>(new IdLookupResult(ids, new None()));

	private static Answer<IdLookupResult> HintsOnly() => new Found<IdLookupResult>(new IdLookupResult([], Some.Of(FromTmdb)));

	private static Answer<IdLookupResult> Down(RemoteService remote) => new Unavailable(remote, new None(), true);
}
