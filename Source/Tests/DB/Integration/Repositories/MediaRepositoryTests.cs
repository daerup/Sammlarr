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
	public async Task GetByIdsAsync_WhenMediaExists_ReturnsAllMappedIds()
	{
		var imdbId = ImdbId.From("tt0101", MediaType.Movie);
		IMediaId[] expectedIds = [TmdbId.From("101", MediaType.Movie), imdbId, TvdbId.From("201", MediaType.Movie)];

		await SetUpMediaAsync(expectedIds);

		var mappedIds = await _testee.GetByIdsAsync([imdbId]);

		await Assert.That(mappedIds).Count().IsEqualTo(expectedIds.Length);
		await AssertContainsIds(mappedIds, expectedIds);
	}

	[Test]
	public async Task GetByIdsAsync_WhenMultipleCandidatesShareMedia_ReturnsIdsWithoutDuplicates()
	{
		var imdbId = ImdbId.From("tt0102", MediaType.Movie);
		var tmdbId = TmdbId.From("102", MediaType.Movie);
		IMediaId[] expectedIds = [tmdbId, imdbId, TvdbId.From("202", MediaType.Movie)];

		await SetUpMediaAsync(expectedIds);

		var mappedIds = await _testee.GetByIdsAsync([imdbId, tmdbId]);

		await Assert.That(mappedIds).Count().IsEqualTo(expectedIds.Length);
		await AssertContainsIds(mappedIds, expectedIds);
	}

	[Test]
	public async Task GetByIdsAsync_WhenNoIdsMatch_ReturnsEmptyList()
	{
		var mappedIds = await _testee.GetByIdsAsync([ImdbId.From("tt-not-found", MediaType.Movie)]);

		await Assert.That(mappedIds.Count).IsEqualTo(0);
	}

	[Test]
	public async Task GetByIdsAsync_WhenEmptyInput_ReturnsEmptyList()
	{
		var mappedIds = await _testee.GetByIdsAsync([]);

		await Assert.That(mappedIds.Count).IsEqualTo(0);
	}

	[Test]
	public async Task CreateAsync_WhenIdsAreUnique_PersistsSingleMedia()
	{
		IMediaId[] ids = [TmdbId.From("111", MediaType.Movie), ImdbId.From("tt0111", MediaType.Movie)];

		await _testee.CreateAsync(ids);

		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		var persistedIds = media.ExternalIds.Select(i => i.ToMediaId());

		await Assert.That(media.ExternalIds).Count().IsEqualTo(2);
		await AssertContainsIds(persistedIds, ids);
	}

	[Test]
	public async Task CreateAsync_WhenInputContainsDuplicateIds_StoresDistinctIdsOnly()
	{
		IMediaId[] ids =
		[
			TmdbId.From("121", MediaType.Movie),
			TmdbId.From("121", MediaType.Movie),
			ImdbId.From("tt0121", MediaType.Movie),
			ImdbId.From("tt0121", MediaType.Movie),
		];
		IMediaId[] expected = [TmdbId.From("121", MediaType.Movie), ImdbId.From("tt0121", MediaType.Movie)];

		await _testee.CreateAsync(ids);

		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		var persistedIds = media.ExternalIds.Select(i => i.ToMediaId());

		await Assert.That(media.ExternalIds).Count().IsEqualTo(2);
		await AssertContainsIds(persistedIds, expected);
	}

	[Test]
	public async Task CreateAsync_WhenIdsContainMultipleMediaTypes_ThrowsArgumentException()
	{
		IMediaId[] ids = [TmdbId.From("121", MediaType.Movie), ImdbId.From("tt0121", MediaType.Show)];

		await Assert.That(async () => await _testee.CreateAsync(ids)).Throws<ArgumentException>();
	}

	[Test]
	public async Task CreateAsync_WhenAnyIdAlreadyMapped_ThrowsDbUpdateException()
	{
		await SetUpMediaAsync([TmdbId.From("131", MediaType.Movie), ImdbId.From("tt0131", MediaType.Movie)]);
		await Assert
			.That(async () => await _testee.CreateAsync([TmdbId.From("131", MediaType.Movie), PlexId.From("plex-131", MediaType.Movie)]))
			.Throws<DbUpdateException>();
	}

	[Test]
	public async Task CreateAsync_WhenIdAlreadyMappedOfDifferentType_DoesNotThrowException()
	{
		await SetUpMediaAsync([TmdbId.From("131", MediaType.Movie), ImdbId.From("tt0131", MediaType.Movie)]);
		await Assert.That(async () => await _testee.CreateAsync([TmdbId.From("131", MediaType.Show), PlexId.From("plex-131", MediaType.Show)])).ThrowsNothing();
	}

	[Test]
	public async Task UpsertAsync_WhenNoMediaExists_CreatesSingleMedia()
	{
		IMediaId[] ids = [TmdbId.From("135", MediaType.Movie), ImdbId.From("tt0135", MediaType.Movie)];

		await _testee.UpsertAsync(ids);

		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		var persistedIds = media.ExternalIds.Select(i => i.ToMediaId());

		await Assert.That(media.ExternalIds).Count().IsEqualTo(2);
		await AssertContainsIds(persistedIds, ids);
	}

	[Test]
	public async Task UpsertAsync_WhenMediaExists_AppendsOnlyMissingIds()
	{
		IMediaId[] initialIds = [TmdbId.From("136", MediaType.Movie), ImdbId.From("tt0136", MediaType.Movie)];
		await SetUpMediaAsync(initialIds);
		fixture.Context.ChangeTracker.Clear();

		IMediaId[] ids = [TmdbId.From("136", MediaType.Movie), PlexId.From("plex-136", MediaType.Movie), PlexId.From("plex-136", MediaType.Movie)];
		IMediaId[] expected = [TmdbId.From("136", MediaType.Movie), ImdbId.From("tt0136", MediaType.Movie), PlexId.From("plex-136", MediaType.Movie)];

		await _testee.UpsertAsync(ids);

		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		var persistedIds = media.ExternalIds.Select(i => i.ToMediaId());

		await Assert.That(media.ExternalIds).Count().IsEqualTo(3);
		await AssertContainsIds(persistedIds, expected);
	}

	[Test]
	public async Task UpsertAsync_WhenIdsContainMultipleMediaTypes_ThrowsArgumentException()
	{
		IMediaId[] ids = [TmdbId.From("136", MediaType.Movie), PlexId.From("plex-136", MediaType.Show)];

		await Assert.That(async () => await _testee.UpsertAsync(ids)).Throws<ArgumentException>();
	}

	[Test]
	public async Task UpsertAsync_WhenIdsMapToDifferentMedia_ThrowsInvalidOperationException()
	{
		await SetUpMediaAsync([TmdbId.From("137", MediaType.Movie), ImdbId.From("tt0137", MediaType.Movie)]);
		await SetUpMediaAsync([TmdbId.From("138", MediaType.Movie), PlexId.From("plex-138", MediaType.Movie)]);
		fixture.Context.ChangeTracker.Clear();

		await Assert
			.That(async () => await _testee.UpsertAsync([TmdbId.From("137", MediaType.Movie), TmdbId.From("138", MediaType.Movie)]))
			.Throws<InvalidOperationException>();
	}

	private async Task SetUpMediaAsync(IEnumerable<IMediaId> ids)
	{
		var media = new MediaEntity();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalId(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
	}

	private static async Task AssertContainsIds(IEnumerable<IMediaId> actualIds, IEnumerable<IMediaId> expectedIds)
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
