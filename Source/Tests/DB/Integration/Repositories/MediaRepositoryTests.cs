using Contracts.Media;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IMediaRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup() => _testee = new MediaRepository(fixture.ContextFactory);

	[Test]
	public async Task FindAsync_WhenMediaExists_ReturnsMediaId()
	{
		var imdbId = ImdbId.From("tt0101", MediaType.Movie);
		IExternalMediaId[] seededIds = [TmdbId.From("101", MediaType.Movie), imdbId, TvdbId.From("201", MediaType.Movie)];

		var seededMediaId = await SetUpMediaAsync(seededIds);

		var identity = await _testee.FindAsync([imdbId]);

		await Assert.That(identity!.Value.Id).IsEqualTo(seededMediaId);
	}

	[Test]
	public async Task FindAsync_WhenMultipleCandidatesShareMedia_ReturnsSingleMediaId()
	{
		var imdbId = ImdbId.From("tt0102", MediaType.Movie);
		var tmdbId = TmdbId.From("102", MediaType.Movie);
		IExternalMediaId[] seededIds = [tmdbId, imdbId, TvdbId.From("202", MediaType.Movie)];

		var seededMediaId = await SetUpMediaAsync(seededIds);

		var identity = await _testee.FindAsync([imdbId, tmdbId]);

		await Assert.That(identity!.Value.Id).IsEqualTo(seededMediaId);
	}

	[Test]
	public async Task FindAsync_WhenNoIdsMatch_ReturnsNull()
	{
		var mediaId = await _testee.FindAsync([ImdbId.From("tt-not-found", MediaType.Movie)]);

		await Assert.That(mediaId).IsNull();
	}

	[Test]
	public async Task FindAsync_WhenCandidatesBridgeDisjointMediaRows_Throws()
	{
		var tmdbId = TmdbId.From("103", MediaType.Movie);
		var imdbId = ImdbId.From("tt0103", MediaType.Movie);
		await SetUpMediaAsync([tmdbId]);
		await SetUpMediaAsync([imdbId]);

		await Assert.That(async () => await _testee.FindAsync([tmdbId, imdbId])).Throws<MediaIdentityConflictException>();
	}

	[Test]
	public async Task FindAsync_WhenEmptyInput_ReturnsNull()
	{
		var mediaId = await _testee.FindAsync([]);

		await Assert.That(mediaId).IsNull();
	}

	[Test]
	public async Task UpsertAsync_WhenIdsAreUnique_PersistsSingleMedia()
	{
		IExternalMediaId[] ids = [TmdbId.From("111", MediaType.Movie), ImdbId.From("tt0111", MediaType.Movie), PlexId.From("plex-111", MediaType.Movie)];

		await _testee.UpsertAsync(ids);

		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		var persistedIds = media.ExternalIds.Select(i => i.ToExternalMediaId());

		await Assert.That(media.ExternalIds).Count().IsEqualTo(3);
		await AssertContainsIds(persistedIds, ids);
	}

	[Test]
	public async Task UpsertAsync_WhenInputContainsDuplicateIds_StoresDistinctIdsOnly()
	{
		IExternalMediaId[] ids =
		[
			TmdbId.From("121", MediaType.Movie),
			TmdbId.From("121", MediaType.Movie),
			ImdbId.From("tt0121", MediaType.Movie),
			ImdbId.From("tt0121", MediaType.Movie),
			PlexId.From("plex-121", MediaType.Movie),
		];
		IExternalMediaId[] expected = [TmdbId.From("121", MediaType.Movie), ImdbId.From("tt0121", MediaType.Movie), PlexId.From("plex-121", MediaType.Movie)];

		await _testee.UpsertAsync(ids);

		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		var persistedIds = media.ExternalIds.Select(i => i.ToExternalMediaId());

		await Assert.That(media.ExternalIds).Count().IsEqualTo(3);
		await AssertContainsIds(persistedIds, expected);
	}

	[Test]
	public async Task UpsertAsync_WhenIdsContainMultipleMediaTypes_ThrowsMixedMediaTypes()
	{
		IExternalMediaId[] ids = [TmdbId.From("121", MediaType.Movie), ImdbId.From("tt0121", MediaType.Show), PlexId.From("plex-121", MediaType.Movie)];

		await Assert.That(async () => await _testee.UpsertAsync(ids)).Throws<MixedMediaTypesException>();
	}

	[Test]
	public async Task UpsertAsync_WhenCandidatesBridgeDisjointMediaRows_Throws()
	{
		var tmdbId = TmdbId.From("122", MediaType.Movie);
		var imdbId = ImdbId.From("tt0122", MediaType.Movie);
		var plexId = PlexId.From("plex-122", MediaType.Movie);
		await SetUpMediaAsync([tmdbId, plexId]);
		await SetUpMediaAsync([imdbId]);

		await Assert.That(async () => await _testee.UpsertAsync([tmdbId, imdbId, plexId])).Throws<MediaIdentityConflictException>();
	}

	[Test]
	public async Task UpsertAsync_WhenNoMediaExists_CreatesSingleMedia()
	{
		IExternalMediaId[] ids = [TmdbId.From("135", MediaType.Movie), ImdbId.From("tt0135", MediaType.Movie), PlexId.From("plex-135", MediaType.Movie)];

		await _testee.UpsertAsync(ids);

		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		var persistedIds = media.ExternalIds.Select(i => i.ToExternalMediaId());

		await Assert.That(media.ExternalIds).Count().IsEqualTo(3);
		await AssertContainsIds(persistedIds, ids);
	}

	[Test]
	public async Task UpsertAsync_WhenMediaExists_AppendsOnlyMissingIds()
	{
		IExternalMediaId[] initialIds = [TmdbId.From("136", MediaType.Movie), ImdbId.From("tt0136", MediaType.Movie), PlexId.From("plex-136", MediaType.Movie)];
		await SetUpMediaAsync(initialIds);
		fixture.Context.ChangeTracker.Clear();

		IExternalMediaId[] ids = [TmdbId.From("136", MediaType.Movie), PlexId.From("plex-136", MediaType.Movie), TvdbId.From("tvdb-136", MediaType.Movie)];
		IExternalMediaId[] expected =
		[
			TmdbId.From("136", MediaType.Movie),
			ImdbId.From("tt0136", MediaType.Movie),
			PlexId.From("plex-136", MediaType.Movie),
			TvdbId.From("tvdb-136", MediaType.Movie),
		];

		await _testee.UpsertAsync(ids);

		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		var persistedIds = media.ExternalIds.Select(i => i.ToExternalMediaId());

		await Assert.That(media.ExternalIds).Count().IsEqualTo(4);
		await AssertContainsIds(persistedIds, expected);
	}

	[Test]
	public async Task GetExternalsAsync_WhenMediaExists_ReturnsExternalForRequestedSource()
	{
		var ids = new IExternalMediaId[] { TmdbId.From("210", MediaType.Movie), PlexId.From("plex-210", MediaType.Movie) };
		var identity = await _testee.UpsertAsync(ids);

		var result = await _testee.GetExternalsAsync([identity.Id], MediaProvider.Plex);

		await Assert.That(result.Count).IsEqualTo(1);
		await Assert.That(result[identity.Id]).IsEqualTo<IExternalMediaId>(PlexId.From("plex-210", MediaType.Movie));
	}

	private async Task<MediaId> SetUpMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaEntity();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	private static async Task AssertContainsIds(IEnumerable<IExternalMediaId> actualIds, IEnumerable<IExternalMediaId> expectedIds)
	{
		var actualIdList = actualIds.ToList();
		var expectedIdList = expectedIds.ToList();
		using var disposable = Assert.Multiple();
		await Assert.That(actualIdList).Count().IsEqualTo(expectedIdList.Count);
		foreach (var expectedId in expectedIdList)
		{
			await Assert.That(actualIdList).Contains(expectedId);
		}
	}
}
