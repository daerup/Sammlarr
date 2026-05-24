using API;
using API.IMDB;
using API.Plex;
using API.TMDB;
using API.TvDB;
using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
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

		await Assert.That(result.Value is Unresolved).IsTrue();
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

		await Assert.That(result.Value is Unresolved).IsTrue();
		await Assert.That(deps.Cache.SetWithTtlCalls).IsEqualTo(0);
	}

	[Test]
	public async Task ResolveAsync_WhenResolvedFromGroupRepository_BypassesNegativeCache()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
		var tvdbId = TvdbId.From("999", MediaType.Movie);
		var (testee, deps) = CreateTestee(cachedIds: [tmdbId, tvdbId]);

		var result = await testee.ResolveAsync([tmdbId], MediaProvider.Tvdb, new None());

		await Assert.That(result.Value is Resolved).IsTrue();
		var resolved = (Resolved)result.Value;
		await Assert.That(resolved.Externals).Contains((IExternalMediaId)tvdbId);
		await Assert.That(resolved.TargetExternal).IsEqualTo<IExternalMediaId>(tvdbId);
		await Assert.That(deps.Cache.SetWithTtlCalls).IsEqualTo(0);
	}

	[Test]
	public async Task ResolveAsync_WhenCacheHitAndCandidatesContainNewIds_AppendsAndIncludesInResponse()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
		var tvdbId = TvdbId.From("999", MediaType.Movie);
		var newImdbId = ImdbId.From("tt0123", MediaType.Movie);
		var (testee, deps) = CreateTestee(cachedIds: [tmdbId, tvdbId], appendIds: [newImdbId]);

		var result = await testee.ResolveAsync([tmdbId, newImdbId], MediaProvider.Tvdb, new None());

		await Assert.That(result.Value is Resolved).IsTrue();
		var resolved = (Resolved)result.Value;
		await Assert.That(resolved.Externals).Contains((IExternalMediaId)tvdbId);
		await Assert.That(resolved.Externals).Contains((IExternalMediaId)newImdbId);
		await Assert.That(resolved.Externals).Contains((IExternalMediaId)tmdbId);
		deps.Repo.UpsertAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task ResolveAsync_WhenCacheHitAndCandidatesAllKnown_DoesNotUpsert()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
		var tvdbId = TvdbId.From("999", MediaType.Movie);
		var (testee, deps) = CreateTestee(cachedIds: [tmdbId, tvdbId]);

		await testee.ResolveAsync([tmdbId], MediaProvider.Tvdb, new None());

		deps.Repo.UpsertAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveAsync_WhenConvertedFresh_MergesNonTargetCandidatesIntoAdditionalIds()
	{
		var tmdbId = TmdbId.From("42", MediaType.Movie);
		var imdbId = ImdbId.From("tt0042", MediaType.Movie);
		var (testee, _) = CreateTestee(appendIds: [tmdbId, imdbId]);

		var result = await testee.ResolveAsync([imdbId, tmdbId], MediaProvider.Tmdb, new None());

		await Assert.That(result.Value is Resolved).IsTrue();
		var resolved = (Resolved)result.Value;
		await Assert.That(resolved.Externals).Contains((IExternalMediaId)tmdbId);
		await Assert.That(resolved.Externals).Contains((IExternalMediaId)imdbId);
		await Assert.That(resolved.TargetExternal).IsEqualTo<IExternalMediaId>(tmdbId);
	}

	[Test]
	public async Task ResolveAsync_WhenConvertedFresh_DoesNotDuplicateTargetInAdditionalIds()
	{
		var tmdbId = TmdbId.From("42", MediaType.Movie);
		var (testee, _) = CreateTestee(appendIds: [tmdbId]);

		var result = await testee.ResolveAsync([tmdbId], MediaProvider.Tmdb, new None());

		await Assert.That(result.Value is Resolved).IsTrue();
		var resolved = (Resolved)result.Value;
		await Assert.That(resolved.Externals.Count).IsEqualTo(1);
		await Assert.That(resolved.Externals).Contains((IExternalMediaId)tmdbId);
		await Assert.That(resolved.TargetExternal).IsEqualTo<IExternalMediaId>(tmdbId);
	}

	private static (MediaIdResolver Testee, Dependencies Deps) CreateTestee(
		IReadOnlyList<IExternalMediaId>? cachedIds = null,
		IReadOnlyList<IExternalMediaId>? appendIds = null
	)
	{
		var repo = IMediaRepository.Mock();
		var mediaId = MediaId.From(Guid.NewGuid());
		IReadOnlyList<IExternalMediaId> cachedSet = (cachedIds ?? []).Distinct().ToList();
		IReadOnlyList<IExternalMediaId> appendedSet = (appendIds is null ? cachedSet : cachedSet.Concat(appendIds)).Distinct().ToList();
		if (cachedIds is not null && cachedIds.Count > 0)
		{
			repo.FindAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(new MediaIdentity(mediaId, cachedSet));
		}
		else
		{
			repo.FindAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns((MediaIdentity?)null);
		}

		repo.UpsertAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(new MediaIdentity(mediaId, appendedSet));

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

	private sealed record Dependencies(Mock<IMediaRepository> Repo, FakeCache Cache);

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
