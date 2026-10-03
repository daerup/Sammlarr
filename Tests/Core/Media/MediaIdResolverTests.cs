using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Core.Media;
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
	private static readonly ById Detail = new(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);
	private static readonly ById PlexById = new(Accepts: [MediaProvider.Plex], Yields: [MediaProvider.Tvdb], Cost: 1);
	private static readonly ByHints Search = new(Yields: [MediaProvider.Plex, MediaProvider.Tvdb], Cost: 4);
	private static readonly IdentifyRequest Request = new([Tmdb], new None());
	private static readonly CancellationToken WaitingThrows = new(canceled: true);

	private readonly Mock<IExternalIdConverter> _tmdb = Converter(MediaProvider.Tmdb, Detail);
	private readonly Mock<IExternalIdConverter> _plex = Converter(MediaProvider.Plex, PlexById, Search);
	private readonly Mock<ITypedMemoryCache<string, bool>> _cache = TestUnmatchedCache.Empty();

	[Test]
	public async Task ResolveAsync_WhenAProviderIsBlockedWithinItsMaxWait_WaitsThenTakesThePath()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee(TmdbBlocked(() => Now.AddMilliseconds(20))).ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhileWaitingForABlockedProvider_HonoursCancellation()
	{
		var testee = Testee(TmdbBlocked(() => Now.AddMilliseconds(20)));

		await Assert.That(async () => await testee.ResolveAsync(Request, MediaProvider.Tvdb, WaitingThrows)).Throws<OperationCanceledException>();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenABlockOutlastsTheMaxWait_GivesUpWithoutCachingTheTitle()
	{
		var resolved = await Testee(TmdbBlocked(() => Now.AddMinutes(1))).ResolveAsync(Request, MediaProvider.Tvdb, WaitingThrows);

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenTheBlockIsExtendedDuringTheWait_GivesUpWithoutCachingTheTitle()
	{
		var pushes = 0;

		var resolved = await Testee(TmdbBlocked(() => Now.AddMilliseconds(20 + pushes++))).ResolveAsync(Request, MediaProvider.Tvdb);

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

		var resolved = await Testee(TmdbBlocked(() => tmdbFreeAt)).ResolveAsync(Request, MediaProvider.Tvdb);

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
					return Error.From(new ProviderUnavailableException(MediaProvider.Tmdb));
				}
			);

		var resolved = await Testee(TmdbBlocked(() => tmdbFreeAt)).ResolveAsync(Request, MediaProvider.Tvdb, WaitingThrows);

		await Assert.That(resolved).IsError();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_AfterASuccessfulWait_CachesAnAnsweredMissAsUnmatched()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Error.From("TMDB knows no TVDB id."));

		var resolved = await Testee(TmdbBlocked(() => Now.AddMilliseconds(20)), _tmdb).ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForTheCheapestOpenPath_ResolvesWithoutTryingTheOthers()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

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

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_HandsTheHintsTmdbFoundToTheFollowingSearch()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex
			.LookUpAsync(Any(), Any())
			.Returns((request, _) => request is ByHintsRequest { Hints: var given } && given == FromTmdb ? Found(Plex) : Error.From("TMDB's hints never arrived."));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Plex);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Plex));
	}

	[Test]
	public async Task ResolveAsync_WhenTwoHopsAskTheSameLookupForTheSameId_CallsItOnce()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex.LookUpAsync(Any(), Any()).Returns(Error.From("Plex found nothing."));

		await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForTwoLookupsOfEqualCost_KeepsTheirAnswersApart()
	{
		var first = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Tvdb], Cost: 1);
		var second = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);
		var tmdb = Converter(MediaProvider.Tmdb, first, second);
		Answer(tmdb, new ByIdRequest(first, Tmdb), Error.From("The first lookup knows no TVDB id."));
		Answer(tmdb, new ByIdRequest(second, Tmdb), Found(Tvdb));

		var resolved = await Testee(tmdb).ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_ForEqualLookupsOfTwoConverters_KeepsTheirAnswersApart()
	{
		var other = Converter(MediaProvider.Imdb, Detail);
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Error.From("TMDB knows no TVDB id."));
		Answer(other, new ByIdRequest(Detail, Tmdb), Found(Tvdb));

		var resolved = await Testee(_tmdb, other).ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
		other.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForASearchWithOtherKnownIds_AsksPlexAgain()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Success.Of(new IdLookupResult([Imdb], Some.Of(FromTmdb))));
		_plex.LookUpAsync(Any(), Any()).Returns(Error.From("Plex found nothing."));

		await Testee().ResolveAsync(new IdentifyRequest([Tmdb], Some.Of(FromTmdb)), MediaProvider.Tvdb);

		_plex.LookUpAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ResolveAsync_WhenTheFirstPlexIdLacksTheWantedId_TriesTheSecond()
	{
		var second = PlexId.From("plex-81189", MediaType.Show);
		Answer(_plex, new ByIdRequest(PlexById, Plex), Found(Plex));
		Answer(_plex, new ByIdRequest(PlexById, second), Found(second, Tvdb));

		var resolved = await Testee().ResolveAsync(new IdentifyRequest([Plex, second], new None()), MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhenTmdbKnowsOnlyTheSecondImdbId_FindsTheTmdbIdThroughIt()
	{
		var find = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tmdb], Cost: 1);
		var tmdb = Converter(MediaProvider.Tmdb, find);
		var unknown = ImdbId.From("tt0000001", MediaType.Show);
		Answer(tmdb, new ByIdRequest(find, unknown), Error.From("TMDB has no match for tt0000001."));
		Answer(tmdb, new ByIdRequest(find, Imdb), Found(Tmdb));

		var resolved = await Testee(tmdb).ResolveAsync(new IdentifyRequest([unknown, Imdb], new None()), MediaProvider.Tmdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tmdb));
	}

	[Test]
	public async Task ResolveAsync_WhenAnAnswerLacksTheWantedId_FailsTheHopAndRunsTheNextPath()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Found(Imdb));
		Answer(_plex, new ByIdRequest(PlexById, Plex), Found(Tvdb));

		var resolved = await Testee().ResolveAsync(new IdentifyRequest([Tmdb, Plex], new None()), MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhenTheCheapestPathIsRateLimited_FallsThroughToAPathAroundIt()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Error.From(new ProviderUnavailableException(MediaProvider.Tmdb)));
		Answer(_plex, new ByIdRequest(PlexById, Plex), Found(Tvdb));

		var resolved = await Testee().ResolveAsync(new IdentifyRequest([Tmdb, Plex], new None()), MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderIsUnavailable_SkipsTheRemainingPathsThroughItAndCachesNothing()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), Error.From(new ProviderUnavailableException(MediaProvider.Tmdb)));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderGetsBlockedWhileResolving_SkipsTheRemainingPathsThroughIt()
	{
		var tmdbBlocked = false;
		var capacity = TestCapacity.With(provider => new ProviderLoad(Optional.When(tmdbBlocked && provider is MediaProvider.Tmdb, () => Now.AddMinutes(1)), 0));
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					tmdbBlocked = true;
					return HintsOnly();
				}
			);

		var resolved = await Testee(capacity).ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderGetsBlockedMidPath_StopsBeforeTheHopThroughIt()
	{
		var plexBlocked = false;
		var capacity = TestCapacity.With(provider => new ProviderLoad(Optional.When(plexBlocked && provider is MediaProvider.Plex, () => Now.AddMinutes(1)), 0));
		_tmdb
			.LookUpAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					plexBlocked = true;
					return HintsOnly();
				}
			);

		var resolved = await Testee(capacity).ResolveAsync(Request, MediaProvider.Plex);

		await Assert.That(resolved).IsError();
		_plex.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenEveryPathFindsNothing_CachesTheTitleAsUnmatched()
	{
		Answer(_tmdb, new ByIdRequest(Detail, Tmdb), HintsOnly());
		_plex.LookUpAsync(Any(), Any()).Returns(Error.From("Plex found nothing."));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenAConverterThrows_TreatsItAsTransient()
	{
		_tmdb.LookUpAsync(Any(), Any()).Throws(new InvalidOperationException("transient"));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenEveryPathRunsThroughABlockedProvider_AsksNobodyAndCachesNothing()
	{
		var blocked = TestCapacity.With(_ => new ProviderLoad(Some.Of(Now.AddMinutes(1)), 0));

		var resolved = await Testee(blocked).ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_ForATitleThatAlreadyHoldsTheTarget_ReturnsItsCandidatesWithoutCachingItAsUnmatched()
	{
		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tmdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).IsEquivalentTo(new IExternalMediaId[] { Tmdb }));
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenNoLookupReachesTheTarget_CachesTheTitleAsUnmatched()
	{
		var resolved = await Testee().ResolveAsync(new IdentifyRequest([MdblistId.From("42", MediaType.Show)], new None()), MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheTitleIsCachedAsUnmatched_AsksNobody()
	{
		_cache.GetValueOrNone(Any()).Returns(Some.Of(true));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_tmdb.LookUpAsync(Any(), Any()).WasNeverCalled();
	}

	private MediaIdResolver Testee(IProviderCapacity? capacity = null) => Testee(capacity ?? TestCapacity.Open(), _tmdb, _plex);

	private MediaIdResolver Testee(params Mock<IExternalIdConverter>[] converters) => Testee(TestCapacity.Open(), converters);

	private MediaIdResolver Testee(IProviderCapacity capacity, params Mock<IExternalIdConverter>[] converters)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(Now);
		return new MediaIdResolver([.. converters.Select(converter => converter.Object)], _cache.Object, capacity, clock.Object);
	}

	private static IProviderCapacity TmdbBlocked(Func<DateTimeOffset> until)
	{
		var maxWait = TimeSpan.FromSeconds(15);
		return TestCapacity.With(provider => provider is MediaProvider.Tmdb ? new ProviderLoad(Some.Of(until()), 0, maxWait) : new ProviderLoad(new None(), 0));
	}

	private static Mock<IExternalIdConverter> Converter(MediaProvider provider, params IdLookup[] lookups)
	{
		var converter = IExternalIdConverter.Mock();
		converter.Provider.Returns(provider);
		converter.Lookups.Returns(lookups);
		return converter;
	}

	private static void Answer(Mock<IExternalIdConverter> converter, IdLookupRequest request, Result<IdLookupResult> answer)
	{
		converter.LookUpAsync(request, Any()).Returns(answer);
	}

	private static Result<IdLookupResult> Found(params IExternalMediaId[] ids) => Success.Of(new IdLookupResult(ids, new None()));

	private static Result<IdLookupResult> HintsOnly() => Success.Of(new IdLookupResult([], Some.Of(FromTmdb)));
}
