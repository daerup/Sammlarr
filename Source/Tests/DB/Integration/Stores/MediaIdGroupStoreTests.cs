using Contracts.Media;
using DB.Data;
using DB.Data.Entities;
using DB.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Stores;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaIdGroupStoreTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IMediaIdGroupStore _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_testee = new MediaIdGroupStore(fixture.Context);
	}

	[Test]
	public async Task GetByIdAsync_WhenGroupExists_ReturnsAllMappedIds()
	{
		var imdbId = ImdbId.From("tt0101", MediaType.Movie);
		IMediaId[] expectedIds = [TmdbId.From("101", MediaType.Movie), imdbId, TvdbId.From("201", MediaType.Movie)];

		await SetUpGroupAsync(expectedIds);

		var mappedIds = await _testee.GetByIdAsync(imdbId);

		await Assert.That(mappedIds).Count().IsEqualTo(expectedIds.Length);
		await AssertContainsIds(mappedIds, expectedIds);
	}

	[Test]
	public async Task GetByIdAsync_WhenIdDoesNotExist_ReturnsEmptyList()
	{
		var mappedIds = await _testee.GetByIdAsync(ImdbId.From("tt-not-found", MediaType.Movie));

		await Assert.That(mappedIds.Count).IsEqualTo(0);
	}

	[Test]
	public async Task CreateAsync_WhenIdsAreUnique_PersistsSingleGroup()
	{
		IMediaId[] ids = [TmdbId.From("111", MediaType.Movie), ImdbId.From("tt0111", MediaType.Movie)];

		await _testee.CreateAsync(ids);

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIdEntries).SingleAsync();
		var persistedIds = group.MediaIdEntries.Select(i => i.Identifier.ToMediaId());

		await Assert.That(group.MediaIdEntries).Count().IsEqualTo(2);
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

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIdEntries).SingleAsync();
		var persistedIds = group.MediaIdEntries.Select(i => i.Identifier.ToMediaId());

		await Assert.That(group.MediaIdEntries).Count().IsEqualTo(2);
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
		await SetUpGroupAsync([TmdbId.From("131", MediaType.Movie), ImdbId.From("tt0131", MediaType.Movie)]);
		await Assert
			.That(async () => await _testee.CreateAsync([TmdbId.From("131", MediaType.Movie), PlexId.From("plex-131", MediaType.Movie)]))
			.Throws<DbUpdateException>();
	}

	[Test]
	public async Task CreateAsync_WhenIdAlreadyMappedOfDifferentType_DoesNotThrowException()
	{
		await SetUpGroupAsync([TmdbId.From("131", MediaType.Movie), ImdbId.From("tt0131", MediaType.Movie)]);
		await Assert.That(async () => await _testee.CreateAsync([TmdbId.From("131", MediaType.Show), PlexId.From("plex-131", MediaType.Show)])).ThrowsNothing();
	}

	[Test]
	public async Task CreateOrAppendAsync_WhenNoGroupExists_CreatesSingleGroup()
	{
		IMediaId[] ids = [TmdbId.From("135", MediaType.Movie), ImdbId.From("tt0135", MediaType.Movie)];

		await _testee.CreateOrAppendAsync(ids);

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIdEntries).SingleAsync();
		var persistedIds = group.MediaIdEntries.Select(i => i.Identifier.ToMediaId());

		await Assert.That(group.MediaIdEntries).Count().IsEqualTo(2);
		await AssertContainsIds(persistedIds, ids);
	}

	[Test]
	public async Task CreateOrAppendAsync_WhenGroupExists_AppendsOnlyMissingIds()
	{
		IMediaId[] initialIds = [TmdbId.From("136", MediaType.Movie), ImdbId.From("tt0136", MediaType.Movie)];
		await SetUpGroupAsync(initialIds);
		fixture.Context.ChangeTracker.Clear();

		IMediaId[] ids = [TmdbId.From("136", MediaType.Movie), PlexId.From("plex-136", MediaType.Movie), PlexId.From("plex-136", MediaType.Movie)];
		IMediaId[] expected = [TmdbId.From("136", MediaType.Movie), ImdbId.From("tt0136", MediaType.Movie), PlexId.From("plex-136", MediaType.Movie)];

		await _testee.CreateOrAppendAsync(ids);

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIdEntries).SingleAsync();
		var persistedIds = group.MediaIdEntries.Select(i => i.Identifier.ToMediaId());

		await Assert.That(group.MediaIdEntries).Count().IsEqualTo(3);
		await AssertContainsIds(persistedIds, expected);
	}

	[Test]
	public async Task CreateOrAppendAsync_WhenIdsContainMultipleMediaTypes_ThrowsArgumentException()
	{
		IMediaId[] ids = [TmdbId.From("136", MediaType.Movie), PlexId.From("plex-136", MediaType.Show)];

		await Assert.That(async () => await _testee.CreateOrAppendAsync(ids)).Throws<ArgumentException>();
	}

	[Test]
	public async Task CreateOrAppendAsync_WhenIdsMapToDifferentGroups_ThrowsInvalidOperationException()
	{
		await SetUpGroupAsync([TmdbId.From("137", MediaType.Movie), ImdbId.From("tt0137", MediaType.Movie)]);
		await SetUpGroupAsync([TmdbId.From("138", MediaType.Movie), PlexId.From("plex-138", MediaType.Movie)]);
		fixture.Context.ChangeTracker.Clear();

		await Assert
			.That(async () => await _testee.CreateOrAppendAsync([TmdbId.From("137", MediaType.Movie), TmdbId.From("138", MediaType.Movie)]))
			.Throws<InvalidOperationException>();
	}

	[Test]
	public async Task AppendAsync_WhenExistingGroupFound_AddsOnlyNewDistinctIds()
	{
		var existingId = TmdbId.From("141", MediaType.Movie);
		IMediaId[] initialIds = [existingId, ImdbId.From("tt0141", MediaType.Movie)];
		await SetUpGroupAsync(initialIds);
		fixture.Context.ChangeTracker.Clear();

		IMediaId[] idsToAppend =
		[
			ImdbId.From("tt0141", MediaType.Movie),
			PlexId.From("plex-141", MediaType.Movie),
			PlexId.From("plex-141", MediaType.Movie),
			TvdbId.From("241", MediaType.Movie),
		];
		IMediaId[] expected =
		[
			existingId,
			ImdbId.From("tt0141", MediaType.Movie),
			PlexId.From("plex-141", MediaType.Movie),
			TvdbId.From("241", MediaType.Movie),
		];

		await _testee.AppendAsync(existingId, idsToAppend);

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIdEntries).SingleAsync();
		var persistedIds = group.MediaIdEntries.Select(i => i.Identifier.ToMediaId());

		await Assert.That(group.MediaIdEntries).Count().IsEqualTo(4);
		await AssertContainsIds(persistedIds, expected);
	}

	[Test]
	public async Task AppendAsync_WhenExistingIdDoesNotExist_ThrowsInvalidOperationException()
	{
		IMediaId[] idsToAppend = [ImdbId.From("tt0151", MediaType.Movie)];

		await Assert.That(async () => await _testee.AppendAsync(TmdbId.From("151", MediaType.Movie), idsToAppend)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task AppendAsync_WhenIdsContainMultipleMediaTypes_ThrowsArgumentException()
	{
		var existingId = TmdbId.From("152", MediaType.Movie);
		await SetUpGroupAsync([existingId, ImdbId.From("tt0152", MediaType.Movie)]);
		fixture.Context.ChangeTracker.Clear();

		IMediaId[] idsToAppend = [PlexId.From("plex-152", MediaType.Movie), TvdbId.From("252", MediaType.Show)];

		await Assert.That(async () => await _testee.AppendAsync(existingId, idsToAppend)).Throws<ArgumentException>();
	}

	[Test]
	public async Task AppendAsync_WhenAppendingIdFromOtherGroup_ThrowsDbUpdateException()
	{
		var anchorGroupId = TmdbId.From("161", MediaType.Movie);
		await SetUpGroupAsync([anchorGroupId, ImdbId.From("tt0161", MediaType.Movie)]);
		await SetUpGroupAsync([TmdbId.From("162", MediaType.Movie), PlexId.From("plex-162", MediaType.Movie)]);
		fixture.Context.ChangeTracker.Clear();

		IMediaId[] idsToAppend = [PlexId.From("plex-162", MediaType.Movie)];

		await Assert.That(async () => await _testee.AppendAsync(anchorGroupId, idsToAppend)).Throws<DbUpdateException>();
	}

	private async Task SetUpGroupAsync(IEnumerable<IMediaId> ids)
	{
		var group = new MediaIdentifierGroup();
		await fixture.Context.MediaIdEntries.AddRangeAsync(
			ids.Select(id => new MediaIdentifierEntry { Identifier = MediaIdentifier.From(id), MediaIdGroup = group })
		);
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
