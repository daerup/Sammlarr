using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Core.Media;
using Test.Shared;

namespace Core.Tests.Unit.Media;

public class MediaServiceTests
{
	private static readonly DateTimeOffset _now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task IdentifyAsync_YieldsOneResultPerInput()
	{
		var first = TmdbId.From("1", MediaType.Movie);
		var second = TmdbId.From("2", MediaType.Movie);
		var third = TmdbId.From("3", MediaType.Movie);

		var builder = new ServiceBuilder().WithResolvedExternal(first).WithResolvedExternal(second).WithResolvedExternal(third);
		var media = builder.Build();

		var requests = new[]
		{
			new ResolveRequest([first], MediaProvider.Tmdb, new None()),
			new ResolveRequest([second], MediaProvider.Tmdb, new None()),
			new ResolveRequest([third], MediaProvider.Tmdb, new None()),
		};

		var results = await media.IdentifyAsync(requests).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(3);
		await Assert.That(results.All(r => r is Identification)).IsTrue();
	}

	[Test]
	public async Task IdentifyAsync_IssuesSingleBatchedMetadataQuery()
	{
		var first = TmdbId.From("10", MediaType.Movie);
		var second = TmdbId.From("11", MediaType.Movie);
		var builder = new ServiceBuilder().WithResolvedExternal(first).WithResolvedExternal(second);
		var media = builder.Build();
		var requests = new[] { new ResolveRequest([first], MediaProvider.Tmdb, new None()), new ResolveRequest([second], MediaProvider.Tmdb, new None()) };

		await media.IdentifyAsync(requests).ToListAsync();

		await Assert.That(builder.MetadataRepository.QueryMediaMetadataCalls).IsEqualTo(1);
	}

	[Test]
	public async Task GetDetailsAsync_ById_YieldsOneResultPerInput()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var firstDetail = BuildDetail(firstId);
		var secondDetail = BuildDetail(secondId);
		var media = new ServiceBuilder().WithMetadata(firstId, firstDetail).WithMetadata(secondId, secondDetail).Build();

		var results = await media.GetDetailsAsync([firstId, secondId]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0].Value).IsSameReferenceAs(firstDetail);
		await Assert.That(results[1].Value).IsSameReferenceAs(secondDetail);
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenMetadataMissing_YieldsError()
	{
		var id = MediaId.From(Guid.NewGuid());
		var media = new ServiceBuilder().Build();

		var results = await media.GetDetailsAsync([id]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0] is Error).IsTrue();
	}

	[Test]
	public async Task GetDetailsAsync_ById_IssuesSingleJoinedReadCall()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var builder = new ServiceBuilder().WithMetadata(firstId, BuildDetail(firstId)).WithMetadata(secondId, BuildDetail(secondId));
		var media = builder.Build();

		await media.GetDetailsAsync([firstId, secondId]).ToListAsync();

		await Assert.That(builder.MetadataRepository.QueryMediaMetadataCalls).IsEqualTo(1);
	}

	[Test]
	public async Task GetDetailsAsync_ById_OnMisses_FlushesMetadataUpsertOnceAtEnd()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var thirdId = MediaId.From(Guid.NewGuid());
		var firstDetail = BuildDetail(firstId);
		var secondDetail = BuildDetail(secondId);
		var thirdDetail = BuildDetail(thirdId);
		var builder = new ServiceBuilder()
			.WithIdentityAndStaleSearchResult(firstId, firstDetail)
			.WithIdentityAndStaleSearchResult(secondId, secondDetail)
			.WithIdentityAndStaleSearchResult(thirdId, thirdDetail);
		var media = builder.Build();

		await media.GetDetailsAsync([firstId, secondId, thirdId]).ToListAsync();

		await Assert.That(builder.MetadataRepository.UpsertManyCalls).IsEqualTo(1);
	}

	[Test]
	public async Task GetDetailsAsync_ByRequest_IdentifiesAndFetches_YieldingMetadataPerRequest()
	{
		var firstExternal = TmdbId.From("100", MediaType.Movie);
		var secondExternal = TmdbId.From("200", MediaType.Movie);
		var builder = new ServiceBuilder().WithResolvedExternal(firstExternal, out var firstDetail).WithResolvedExternal(secondExternal, out var secondDetail);
		var media = builder.Build();

		var requests = new[]
		{
			new ResolveRequest([firstExternal], MediaProvider.Tmdb, new None()),
			new ResolveRequest([secondExternal], MediaProvider.Tmdb, new None()),
		};

		var results = await media.GetDetailsAsync(requests).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0].Value).IsSameReferenceAs(firstDetail);
		await Assert.That(results[1].Value).IsSameReferenceAs(secondDetail);
	}

	[Test]
	public async Task GetDetailsAsync_ByRequest_WhenIdentifyFails_YieldsErrorWithoutFetching()
	{
		var external = TmdbId.From("999", MediaType.Movie);
		var builder = new ServiceBuilder().WithUnresolvable(external);
		var media = builder.Build();

		var requests = new[] { new ResolveRequest([external], MediaProvider.Tmdb, new None()) };
		var results = await media.GetDetailsAsync(requests).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0] is Error).IsTrue();
		await Assert.That(builder.MetadataRepository.UpsertManyCalls).IsEqualTo(0);
	}

	[Test]
	public async Task ResolveIdentitiesAsync_MintsNewIdentities_ForUnknownExternals()
	{
		var external = TmdbId.From("9001", MediaType.Movie);
		var builder = new ServiceBuilder();
		var media = builder.Build();

		var identities = await media.ResolveIdentitiesAsync([
			[external],
		]);

		await Assert.That(identities.Count).IsEqualTo(1);
		await Assert.That(identities[0].Id).IsNotEqualTo(default(MediaId));
		await Assert.That(identities[0].Externals).Contains(external);
		await Assert.That(builder.MediaRepository.UpsertManyCalls).IsEqualTo(1);
	}

	[Test]
	public async Task ResolveIdentitiesAsync_ReusesPersistedMediaId_WhenExternalKnown()
	{
		var external = TmdbId.From("9002", MediaType.Movie);
		var existingId = MediaId.From(Guid.NewGuid());
		var builder = new ServiceBuilder();
		builder.MediaRepository.Register(new MediaIdentity(existingId, [external]));
		var media = builder.Build();

		var identities = await media.ResolveIdentitiesAsync([
			[external],
		]);

		await Assert.That(identities[0].Id).IsEqualTo(existingId);
	}

	[Test]
	public async Task ResolveIdentitiesAsync_UnionsExternals_WhenDatabaseMatchHasOverlap()
	{
		var sharedTmdb = TmdbId.From("9003", MediaType.Movie);
		var dbImdb = ImdbId.From("tt9003", MediaType.Movie);
		var freshPlex = PlexId.From("plex-9003", MediaType.Movie);
		var existingId = MediaId.From(Guid.NewGuid());
		var builder = new ServiceBuilder();
		builder.MediaRepository.Register(new MediaIdentity(existingId, [sharedTmdb, dbImdb]));
		var media = builder.Build();

		var identities = await media.ResolveIdentitiesAsync([
			[sharedTmdb, freshPlex],
		]);

		await Assert.That(identities[0].Id).IsEqualTo(existingId);
		await Assert.That(identities[0].Externals).Contains(sharedTmdb);
		await Assert.That(identities[0].Externals).Contains(dbImdb);
		await Assert.That(identities[0].Externals).Contains(freshPlex);
	}

	[Test]
	public async Task ResolveIdentitiesAsync_WhenEmpty_DoesNotWriteToRepository()
	{
		var builder = new ServiceBuilder();
		var media = builder.Build();

		var identities = await media.ResolveIdentitiesAsync([]);

		await Assert.That(identities).IsEmpty();
		await Assert.That(builder.MediaRepository.UpsertManyCalls).IsEqualTo(0);
	}

	[Test]
	public async Task InvalidateAsync_DelegatesToMetadataResolver()
	{
		var ids = new[] { MediaId.From(Guid.NewGuid()), MediaId.From(Guid.NewGuid()) };
		var builder = new ServiceBuilder();
		var media = builder.Build();

		await media.InvalidateAsync(ids);

		await Assert.That(builder.MetadataRepository.RemoveManyCalls).IsEqualTo(1);
	}

	private static MovieDetail BuildDetail(MediaId id) =>
		new()
		{
			Identity = new MediaIdentity(id, [PlexId.From($"plex-{id}", MediaType.Movie)]),
			Title = "Test",
			Tagline = "Tag",
			Summary = "Summary",
			PosterUrl = "https://example.com/p.jpg",
			BackdropUrl = "https://example.com/b.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

	private sealed class ServiceBuilder
	{
		private readonly Dictionary<IExternalMediaId, IMediaDetail> _searchResults = new();

		public FakeBatchMediaRepository MediaRepository { get; } = new();
		public FakeBatchMetadataRepository MetadataRepository { get; } = new();
		public PrimedCache UnresolvedCache { get; } = new();

		public ServiceBuilder()
		{
			MetadataRepository.BindMediaRepository(MediaRepository);
		}

		public ServiceBuilder WithResolvedExternal(IExternalMediaId external)
		{
			var mediaId = MediaId.From(Guid.NewGuid());
			MediaRepository.Register(new MediaIdentity(mediaId, [external]));
			return this;
		}

		public ServiceBuilder WithResolvedExternal(IExternalMediaId external, out IMediaDetail detail)
		{
			var mediaId = MediaId.From(Guid.NewGuid());
			var built = BuildDetail(mediaId);
			var identity = new MediaIdentity(mediaId, [external, .. built.Identity.Externals]);
			detail = built with { Identity = identity };
			MediaRepository.Register(identity);
			MetadataRepository.Register(mediaId, detail, _now);
			return this;
		}

		public ServiceBuilder WithMetadata(MediaId id, IMediaDetail detail)
		{
			MetadataRepository.Register(id, detail, _now);
			MediaRepository.Register(new MediaIdentity(id, detail.Identity.Externals));
			return this;
		}

		public ServiceBuilder WithIdentityAndStaleSearchResult(MediaId id, IMediaDetail detail)
		{
			MediaRepository.Register(new MediaIdentity(id, detail.Identity.Externals));
			foreach (var external in detail.Identity.Externals)
			{
				_searchResults[external] = detail;
			}
			return this;
		}

		public ServiceBuilder WithUnresolvable(IExternalMediaId external)
		{
			UnresolvedCache.Primed.Add($"{MediaProvider.Tmdb}|{external}");
			return this;
		}

		public IMediaService Build()
		{
			var dateTimeProvider = IDateTimeProvider.Mock();
			dateTimeProvider.Now.Returns(_now);
			var idResolver = new MediaIdResolver(null, UnresolvedCache);

			var search = IMediaSearchProvider.Mock();
			if (_searchResults.Count > 0)
			{
				search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((external, _) => _searchResults[external]);
			}
			var enricher = IMetadataEnricher.Mock();
			var rating = IMediaRatingService.Mock();
			var configRepository = IProviderConfigRepository.Mock();
			configRepository
				.GetAsync(Any())
				.Returns(
					new ProviderConfig
					{
						WatchlistProvider = WatchlistProvider.Plex,
						TrendingProvider = MediaRecommendationProvider.Tmdb,
						SearchProvider = MediaSearchProvider.Plex,
						RatingProvider = RatingProvider.Tmdb,
					}
				);

			var searchIndex = new FakeKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider>().Register(MediaSearchProvider.Plex, search.Object);
			var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IMetadataEnricher>()
				.Register(MediaProvider.Plex, enricher.Object)
				.Register(MediaProvider.Tmdb, enricher.Object);
			var ratingIndex = new FakeKeyedServiceIndex<RatingProvider, IMediaRatingService>().Register(RatingProvider.Tmdb, rating.Object);

			var metadataResolver = new MediaMetadataResolver(searchIndex, enricherIndex, ratingIndex, configRepository.Object);
			var coordinator = new MediaIdentityCoordinator(MediaRepository);
			return new MediaService(
				idResolver,
				metadataResolver,
				coordinator,
				MediaRepository,
				MetadataRepository,
				new FakeMediaQuerySessionFactory(),
				dateTimeProvider.Object
			);
		}
	}
}

internal sealed class PrimedCache : ITypedMemoryCache<string, bool>
{
	public HashSet<string> Primed { get; } = new();

	public bool TryGetValue(string key, out bool value)
	{
		if (Primed.Contains(key))
		{
			value = true;
			return true;
		}
		value = false;
		return false;
	}

	public void Set(string key, bool value) => Primed.Add(key);

	public void Set(string key, bool value, TimeSpan absoluteExpirationRelativeToNow) => Primed.Add(key);

	public void Remove(string key) => Primed.Remove(key);
}

internal sealed class FakeMediaQuerySession : IMediaQuerySession
{
	public List<MediaId> MediaIds { get; } = new();

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeMediaQuerySessionFactory : IMediaQuerySessionFactory
{
	public ValueTask<IMediaQuerySession> CreateAsync(CancellationToken cancellationToken = default) =>
		ValueTask.FromResult<IMediaQuerySession>(new FakeMediaQuerySession());
}

internal sealed class FakeBatchMediaRepository : IMediaRepository
{
	private readonly Dictionary<IExternalMediaId, MediaIdentity> _byExternal = new();
	private readonly Dictionary<MediaId, MediaIdentity> _byId = new();

	public int FindManyByGroupsCalls { get; private set; }
	public int UpsertManyCalls { get; private set; }
	public int QueryMediaIdsByExternalsCalls { get; private set; }
	public int QueryMediaIdsByIdsCalls { get; private set; }

	public void Register(MediaIdentity identity)
	{
		RegisterByExternals(identity);
		RegisterByMediaId(identity);
	}

	public void RegisterByExternals(MediaIdentity identity)
	{
		foreach (var external in identity.Externals)
		{
			_byExternal[external] = identity;
		}
	}

	public void RegisterByMediaId(MediaIdentity identity)
	{
		_byId[identity.Id] = identity;
	}

	public IReadOnlyDictionary<MediaId, MediaIdentity> Snapshot => _byId;

	public Task<IReadOnlyList<MediaIdentity?>> FindManyAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	)
	{
		FindManyByGroupsCalls++;
		var result = new MediaIdentity?[candidateGroups.Count];
		for (var i = 0; i < candidateGroups.Count; i++)
		{
			MediaIdentity? match = null;
			foreach (var candidate in candidateGroups[i])
			{
				if (_byExternal.TryGetValue(candidate, out var found))
				{
					if (match.HasValue && match.Value.Id != found.Id)
					{
						throw new MediaIdentityConflictException([match.Value.Id, found.Id], [.. candidateGroups[i]]);
					}
					match = found;
				}
			}
			result[i] = match;
		}
		return Task.FromResult<IReadOnlyList<MediaIdentity?>>(result);
	}

	public Task<IReadOnlyList<MediaIdentity>> UpsertManyAsync(IReadOnlyList<MediaIdentity> identities, CancellationToken cancellationToken = default)
	{
		UpsertManyCalls++;
		var result = new MediaIdentity[identities.Count];
		for (var i = 0; i < identities.Count; i++)
		{
			var input = identities[i];
			var externals = input.Externals.Distinct().ToList();
			var existing = externals.Select(e => _byExternal.TryGetValue(e, out var found) ? (MediaIdentity?)found : null).FirstOrDefault(m => m is not null);
			MediaIdentity stored;
			if (existing is { } match)
			{
				if (match.Id != input.Id)
				{
					throw new MediaIdentityConflictException([match.Id, input.Id], externals);
				}
				stored = match with { Externals = [.. match.Externals, .. externals.Where(e => !match.Externals.Contains(e))] };
			}
			else
			{
				stored = new MediaIdentity(input.Id, externals);
			}
			Register(stored);
			result[i] = stored;
		}
		return Task.FromResult<IReadOnlyList<MediaIdentity>>(result);
	}

	public Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetExternalsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider source,
		CancellationToken cancellationToken = default
	) => throw new NotImplementedException();

	public IQueryable<Guid> QueryMediaIds(IMediaQuerySession session, IEnumerable<IExternalMediaId> candidates)
	{
		QueryMediaIdsByExternalsCalls++;
		var fake = (FakeMediaQuerySession)session;
		var matched = candidates.Where(c => _byExternal.ContainsKey(c)).Select(c => _byExternal[c].Id).Distinct().ToList();
		fake.MediaIds.AddRange(matched);
		return matched.Select(id => id.Value).AsQueryable();
	}

	public IQueryable<Guid> QueryMediaIds(IMediaQuerySession session, IEnumerable<MediaId> ids)
	{
		QueryMediaIdsByIdsCalls++;
		var fake = (FakeMediaQuerySession)session;
		var list = ids.ToList();
		fake.MediaIds.AddRange(list);
		return list.Select(id => id.Value).AsQueryable();
	}
}

internal sealed class FakeBatchMetadataRepository : IMediaMetadataRepository
{
	private readonly Dictionary<MediaId, (IMediaDetail Detail, DateTimeOffset FetchedAt)> _store = new();
	private FakeBatchMediaRepository? _mediaRepository;

	public int QueryMediaMetadataCalls { get; private set; }
	public int UpsertManyCalls { get; private set; }
	public int RemoveManyCalls { get; private set; }

	public void BindMediaRepository(FakeBatchMediaRepository mediaRepository) => _mediaRepository = mediaRepository;

	public void Register(MediaId id, IMediaDetail details, DateTimeOffset fetchedAt) => _store[id] = (details, fetchedAt);

	public async Task<IReadOnlyList<CachedMedia>> QueryMediaMetadataAsync(
		IMediaQuerySession session,
		IQueryable<Guid> mediaIds,
		CancellationToken cancellationToken = default
	)
	{
		QueryMediaMetadataCalls++;
		var guids = mediaIds.ToList();
		await Task.Yield();
		var result = new List<CachedMedia>();
		foreach (var guid in guids)
		{
			var mediaId = MediaId.From(guid);
			if (_mediaRepository?.Snapshot.TryGetValue(mediaId, out var identity) != true)
			{
				continue;
			}
			result.Add(
				_store.TryGetValue(mediaId, out var cached)
					? new CachedMedia(Align(cached.Detail, identity), cached.FetchedAt)
					: new CachedMedia(
						new UnresolvedMedia
						{
							Identity = identity,
							MediaType = MediaType.Unknown,
							Title = string.Empty,
							ReleaseDate = DateOnly.MinValue,
						},
						null
					)
			);
		}
		return result;
	}

	private static IMediaDetail Align(IMediaDetail detail, MediaIdentity identity) =>
		ReferenceEquals(detail.Identity.Externals, identity.Externals)
			? detail
			: detail switch
			{
				MovieDetail m => m with { Identity = identity },
				ShowDetail s => s with { Identity = identity },
				UnresolvedMedia u => u with { Identity = identity },
			};

	public Task UpsertManyAsync(IReadOnlyList<MediaMetadataUpsert> items, CancellationToken cancellationToken = default)
	{
		UpsertManyCalls++;
		return Task.CompletedTask;
	}

	public Task RemoveManyAsync(IReadOnlyList<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		RemoveManyCalls++;
		return Task.CompletedTask;
	}

	public Task RemoveRatingsBySourceAsync(RatingSource source, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
