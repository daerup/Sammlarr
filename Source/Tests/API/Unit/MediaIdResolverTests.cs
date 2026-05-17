using API;
using API.IMDB;
using API.Plex;
using API.TMDB;
using API.TvDB;
using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Convertions;
using Test.Shared;

namespace API.Tests.Unit;

public class MediaIdResolverTests
{
	[Test]
	public async Task ResolveAsync_WhenConverterReturnsUnresolved_CachesNegativeResultWithTtl()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
		var (testee, deps) = CreateTestee();

		var result = await testee.ResolveAsync([tmdbId], MediaProvider.Tvdb, new None());

		await Assert.That(result).IsOfUnionType<ConversionResult, Unresolved>();
		await Assert.That(deps.Cache.SetWithTtlCalls).IsEqualTo(1);
		await Assert.That(deps.Cache.LastTtl).IsEqualTo(TimeSpan.FromHours(1));
	}

	[Test]
	public async Task ResolveAsync_WhenUnresolvedCached_ShortCircuitsAndDoesNotRewriteCache()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		var preWarmKey = $"{MediaProvider.Tvdb}|{tmdbId.Source}:{tmdbId.Type}:{tmdbId.Value}";
		deps.Cache.Store[preWarmKey] = true;

		var result = await testee.ResolveAsync([tmdbId], MediaProvider.Tvdb, new None());

		await Assert.That(result).IsOfUnionType<ConversionResult, Unresolved>();
		await Assert.That(deps.Cache.SetWithTtlCalls).IsEqualTo(0);
	}

	[Test]
	public async Task ResolveAsync_WhenResolvedFromGroupRepository_BypassesNegativeCache()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
		var tvdbId = TvdbId.From("999", MediaType.Movie);
		var (testee, deps) = CreateTestee(cachedIds: [tmdbId, tvdbId]);

		var result = await testee.ResolveAsync([tmdbId], MediaProvider.Tvdb, new None());

		await Assert.That(result).IsOfUnionType<ConversionResult, Resolved>().And.Multiple(async resolved => await Assert.That(resolved.Id).IsEqualTo(tvdbId));
		await Assert.That(deps.Cache.SetWithTtlCalls).IsEqualTo(0);
	}

	[Test]
	public async Task ResolveAsync_WhenCacheHitAndCandidatesContainNewIds_AppendsAndIncludesInResponse()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
		var tvdbId = TvdbId.From("999", MediaType.Movie);
		var newImdbId = ImdbId.From("tt0123", MediaType.Movie);
		var (testee, deps) = CreateTestee(cachedIds: [tmdbId, tvdbId]);

		var result = await testee.ResolveAsync([tmdbId, newImdbId], MediaProvider.Tvdb, new None());

		await Assert
			.That(result)
			.IsOfUnionType<ConversionResult, Resolved>()
			.And.Multiple(async resolved =>
			{
				await Assert.That(resolved.Id).IsEqualTo(tvdbId);
				await Assert.That(resolved.AdditionalIds).Contains((IMediaId)newImdbId);
				await Assert.That(resolved.AdditionalIds).Contains((IMediaId)tmdbId);
			});
		deps.Repo.UpsertAsync(Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenCacheHitAndCandidatesAllKnown_DoesNotUpsert()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
		var tvdbId = TvdbId.From("999", MediaType.Movie);
		var (testee, deps) = CreateTestee(cachedIds: [tmdbId, tvdbId]);

		await testee.ResolveAsync([tmdbId], MediaProvider.Tvdb, new None());

		deps.Repo.UpsertAsync(Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Never);
	}

	private static (MediaIdResolver Testee, Dependencies Deps) CreateTestee(IReadOnlyList<IMediaId>? cachedIds = null)
	{
		var repo = IMediaIdGroupRepository.Mock();
		repo.GetByIdsAsync(Any(), Any()).Returns([.. cachedIds ?? []]);
		var cache = new FakeCache();
		var testee = new MediaIdResolver(
			repo.Object,
			new TmdbIdConverter(null!),
			new PlexIdConverter(null!, null!, null!),
			new ImdbIdConverter(null!),
			new TvdbIdConverter(null!),
			cache
		);
		return (testee, new Dependencies(repo, cache));
	}

	private sealed record Dependencies(Mock<IMediaIdGroupRepository> Repo, FakeCache Cache);

	private sealed class FakeCache : ITypedMemoryCache<string, bool>
	{
		public Dictionary<string, bool> Store { get; } = new();
		public int SetCalls { get; private set; }
		public int SetWithTtlCalls { get; private set; }
		public TimeSpan? LastTtl { get; private set; }

		public bool TryGetValue(string key, out bool value) => Store.TryGetValue(key, out value);

		public void Set(string key, bool value)
		{
			SetCalls++;
			Store[key] = value;
		}

		public void Set(string key, bool value, TimeSpan absoluteExpirationRelativeToNow)
		{
			SetWithTtlCalls++;
			LastTtl = absoluteExpirationRelativeToNow;
			Store[key] = value;
		}

		public void Remove(string key) => Store.Remove(key);
	}
}
