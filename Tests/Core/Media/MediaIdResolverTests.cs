using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Conversions;
using Core.Media;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Media;

public class MediaIdResolverTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly TmdbId Tmdb = TmdbId.From("1396", MediaType.Show);
	private static readonly PlexId Plex = PlexId.From("plex-1396", MediaType.Show);
	private static readonly TvdbId Tvdb = TvdbId.From("81189", MediaType.Show);
	private static readonly IdRoute TmdbToTvdb = new(Some.Of(MediaProvider.Tmdb), MediaProvider.Tvdb, [MediaProvider.Tmdb], 1);
	private static readonly IdRoute PlexToTvdb = new(Some.Of(MediaProvider.Plex), MediaProvider.Tvdb, [MediaProvider.Plex], 1);
	private static readonly IdRoute TmdbToPlex = new(Some.Of(MediaProvider.Tmdb), MediaProvider.Plex, [MediaProvider.Tmdb, MediaProvider.Plex], 5);
	private static readonly ResolveRequest Request = new([Tmdb], new None());

	private readonly Mock<IExternalIdConverter> _tvdb = Converter(TmdbToTvdb, PlexToTvdb);
	private readonly Mock<IExternalIdConverter> _plex = Converter(TmdbToPlex);
	private readonly Mock<ITypedMemoryCache<string, bool>> _cache = ITypedMemoryCache<string, bool>.Mock();

	[Test]
	public async Task ResolveAsync_ForTheCheapestOpenPath_ResolvesWithoutTryingTheOthers()
	{
		Answer(_tvdb, TmdbToTvdb, Success.Of<IReadOnlyList<IExternalMediaId>>([Tvdb]));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).IsEquivalentTo(new IExternalMediaId[] { Tmdb, Tvdb }));
		_plex.ConvertAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenTheCheapestPathFindsNothing_FollowsTheNextPathHopByHop()
	{
		Answer(_tvdb, TmdbToTvdb, Error.From("TMDB knows no TVDB id."));
		Answer(_plex, TmdbToPlex, Success.Of<IReadOnlyList<IExternalMediaId>>([Plex]));
		_tvdb
			.ConvertAsync(PlexToTvdb, Any(), Any(), Any())
			.Returns(
				(_, candidates, _, _) => candidates.Contains(Plex) ? Success.Of<IReadOnlyList<IExternalMediaId>>([Tvdb]) : Error.From("The Plex id never arrived.")
			);

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
	}

	[Test]
	public async Task ResolveAsync_WhenTheCheapestPathIsRateLimited_FallsThroughToAPathAroundIt()
	{
		Answer(_tvdb, TmdbToTvdb, Error.From(new ProviderUnavailableException(MediaProvider.Tmdb)));
		Answer(_tvdb, PlexToTvdb, Success.Of<IReadOnlyList<IExternalMediaId>>([Tvdb]));

		var resolved = await Testee().ResolveAsync(new ResolveRequest([Tmdb, Plex], new None()), MediaProvider.Tvdb);

		await Assert.That(resolved).IsSuccess().And.Multiple(async ids => await Assert.That(ids).Contains(Tvdb));
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderIsUnavailable_SkipsTheRemainingPathsThroughItAndCachesNothing()
	{
		Answer(_tvdb, TmdbToTvdb, Error.From(new ProviderUnavailableException(MediaProvider.Tmdb)));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_plex.ConvertAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderGetsBlockedWhileResolving_SkipsTheRemainingPathsThroughIt()
	{
		var tmdbBlocked = false;
		var capacity = TestCapacity.With(provider => new ProviderLoad(tmdbBlocked && provider is MediaProvider.Tmdb ? Some.Of(Now.AddMinutes(1)) : new None(), 0));
		_tvdb
			.ConvertAsync(TmdbToTvdb, Any(), Any(), Any())
			.Returns(
				(_, _, _, _) =>
				{
					tmdbBlocked = true;
					return Error.From("TMDB knows no TVDB id.");
				}
			);

		var resolved = await Testee(capacity).ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_plex.ConvertAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenAProviderGetsBlockedMidPath_StopsBeforeTheHopThroughIt()
	{
		var plexBlocked = false;
		var capacity = TestCapacity.With(provider => new ProviderLoad(plexBlocked && provider is MediaProvider.Plex ? Some.Of(Now.AddMinutes(1)) : new None(), 0));
		Answer(_tvdb, TmdbToTvdb, Error.From("TMDB knows no TVDB id."));
		_plex
			.ConvertAsync(TmdbToPlex, Any(), Any(), Any())
			.Returns(
				(_, _, _, _) =>
				{
					plexBlocked = true;
					return Success.Of<IReadOnlyList<IExternalMediaId>>([Plex]);
				}
			);

		var resolved = await Testee(capacity).ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_tvdb.ConvertAsync(PlexToTvdb, Any(), Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenEveryPathFindsNothing_CachesTheTitleAsUnmatched()
	{
		Answer(_tvdb, TmdbToTvdb, Error.From("TMDB knows no TVDB id."));
		Answer(_plex, TmdbToPlex, Error.From("Plex found nothing."));

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenAConverterThrows_TreatsItAsTransient()
	{
		_tvdb.ConvertAsync(Any(), Any(), Any(), Any()).Throws(new InvalidOperationException("transient"));
		Answer(_plex, TmdbToPlex, Error.From("Plex found nothing."));

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
		_tvdb.ConvertAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
		_cache.Set(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenNoRouteReachesTheTarget_CachesTheTitleAsUnmatched()
	{
		var resolved = await Testee().ResolveAsync(new ResolveRequest([MdblistId.From("42", MediaType.Show)], new None()), MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheTitleIsCachedAsUnmatched_AsksNobody()
	{
		_cache.TryGetValue(Any()).Returns(true);

		var resolved = await Testee().ResolveAsync(Request, MediaProvider.Tvdb);

		await Assert.That(resolved).IsError();
		_tvdb.ConvertAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	private MediaIdResolver Testee(Contracts.Providers.IProviderCapacity? capacity = null)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(Now);
		return new MediaIdResolver([_tvdb.Object, _plex.Object], _cache.Object, capacity ?? TestCapacity.Open(), clock.Object);
	}

	private static Mock<IExternalIdConverter> Converter(params IdRoute[] routes)
	{
		var converter = IExternalIdConverter.Mock();
		converter.Routes.Returns(routes);
		return converter;
	}

	private static void Answer(Mock<IExternalIdConverter> converter, IdRoute route, Result<IReadOnlyList<IExternalMediaId>> answer) =>
		converter.ConvertAsync(route, Any(), Any(), Any()).Returns(answer);
}
