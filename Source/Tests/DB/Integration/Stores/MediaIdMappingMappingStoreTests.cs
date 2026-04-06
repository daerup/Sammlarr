using Contracts.Media;
using DB.Data;
using DB.Data.Entities;
using DB.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Stores;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaIdMappingMappingStoreTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IMediaIdMappingStore _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_testee = new MediaIdMappingMappingStore(fixture.Context);
	}

	[Test]
	public async Task GetByIdAsync_WhenGroupExists_ReturnsAllMappedIds()
	{
		var imdbId = ImdbId.From("tt0101");
		IMediaId[] expectedIds = [TmdbId.From(101), imdbId, TvdbId.From(201)];

		await SetUpGroupAsync(expectedIds);

		var mappedIds = await _testee.GetByIdAsync(imdbId);

		await Assert.That(mappedIds).IsEquivalentTo(expectedIds);
	}

	[Test]
	public async Task GetByIdAsync_WhenIdDoesNotExist_ReturnsEmptyList()
	{
		var mappedIds = await _testee.GetByIdAsync(ImdbId.From("tt-not-found"));

		await Assert.That(mappedIds.Count).IsEqualTo(0);
	}

	[Test]
	public async Task CreateAsync_WhenIdsAreUnique_PersistsSingleGroup()
	{
		IMediaId[] ids = [TmdbId.From(111), ImdbId.From("tt0111")];

		await _testee.CreateAsync(ids);

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIds).SingleAsync();

		await Assert.That(group.MediaIds).Count().IsEqualTo(2);
		await Assert.That(group.MediaIds.Select(i => i.MapValueObject())).IsEquivalentTo(ids);
	}

	[Test]
	public async Task CreateAsync_WhenInputContainsDuplicateIds_StoresDistinctIdsOnly()
	{
		IMediaId[] ids = [TmdbId.From(121), TmdbId.From(121), ImdbId.From("tt0121"), ImdbId.From("tt0121")];
		IMediaId[] expected = [TmdbId.From(121), ImdbId.From("tt0121")];

		await _testee.CreateAsync(ids);

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIds).SingleAsync();

		await Assert.That(group.MediaIds).Count().IsEqualTo(2);
		await Assert.That(group.MediaIds.Select(i => i.MapValueObject())).IsEquivalentTo(expected);
	}

	[Test]
	public async Task CreateAsync_WhenAnyIdAlreadyMapped_ThrowsInvalidOperationException()
	{
		await SetUpGroupAsync([TmdbId.From(131), ImdbId.From("tt0131")]);

		await Assert.That(async () => await _testee.CreateAsync([TmdbId.From(131), PlexId.From("plex-131")])).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task CreateOrAppendAsync_WhenNoGroupExists_CreatesSingleGroup()
	{
		IMediaId[] ids = [TmdbId.From(135), ImdbId.From("tt0135")];

		await _testee.CreateOrAppendAsync(ids);

		var all = await fixture.Context.MediaIdGroups.Include(g => g.MediaIds).ToListAsync();
		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIds).SingleAsync();

		await Assert.That(group.MediaIds).Count().IsEqualTo(2);
		await Assert.That(group.MediaIds.Select(i => i.MapValueObject())).IsEquivalentTo(ids);
	}

	[Test]
	public async Task CreateOrAppendAsync_WhenGroupExists_AppendsOnlyMissingIds()
	{
		IMediaId[] initialIds = [TmdbId.From(136), ImdbId.From("tt0136")];
		await SetUpGroupAsync(initialIds);
		fixture.Context.ChangeTracker.Clear();

		IMediaId[] ids = [TmdbId.From(136), PlexId.From("plex-136"), PlexId.From("plex-136")];
		IMediaId[] expected = [TmdbId.From(136), ImdbId.From("tt0136"), PlexId.From("plex-136")];

		await _testee.CreateOrAppendAsync(ids);

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIds).SingleAsync();

		await Assert.That(group.MediaIds).Count().IsEqualTo(3);
		await Assert.That(group.MediaIds.Select(i => i.MapValueObject())).IsEquivalentTo(expected);
	}

	[Test]
	public async Task CreateOrAppendAsync_WhenIdsMapToDifferentGroups_ThrowsInvalidOperationException()
	{
		await SetUpGroupAsync([TmdbId.From(137), ImdbId.From("tt0137")]);
		await SetUpGroupAsync([TmdbId.From(138), PlexId.From("plex-138")]);
		fixture.Context.ChangeTracker.Clear();

		await Assert.That(async () => await _testee.CreateOrAppendAsync([TmdbId.From(137), TmdbId.From(138)])).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task AppendAsync_WhenExistingGroupFound_AddsOnlyNewDistinctIds()
	{
		var existingId = TmdbId.From(141);
		IMediaId[] initialIds = [existingId, ImdbId.From("tt0141")];
		await SetUpGroupAsync(initialIds);
		fixture.Context.ChangeTracker.Clear();

		IMediaId[] idsToAppend = [ImdbId.From("tt0141"), PlexId.From("plex-141"), PlexId.From("plex-141"), TvdbId.From(241)];
		IMediaId[] expected = [existingId, ImdbId.From("tt0141"), PlexId.From("plex-141"), TvdbId.From(241)];

		await _testee.AppendAsync(existingId, idsToAppend);

		var group = await fixture.Context.MediaIdGroups.Include(g => g.MediaIds).SingleAsync();

		await Assert.That(group.MediaIds).Count().IsEqualTo(4);
		await Assert.That(group.MediaIds.Select(i => i.MapValueObject())).IsEquivalentTo(expected);
	}

	[Test]
	public async Task AppendAsync_WhenExistingIdDoesNotExist_ThrowsInvalidOperationException()
	{
		IMediaId[] idsToAppend = [ImdbId.From("tt0151")];

		await Assert.That(async () => await _testee.AppendAsync(TmdbId.From(151), idsToAppend)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task AppendAsync_WhenAppendingIdFromOtherGroup_ThrowsDbUpdateException()
	{
		var anchorGroupId = TmdbId.From(161);
		await SetUpGroupAsync([anchorGroupId, ImdbId.From("tt0161")]);
		await SetUpGroupAsync([TmdbId.From(162), PlexId.From("plex-162")]);
		fixture.Context.ChangeTracker.Clear();

		IMediaId[] idsToAppend = [PlexId.From("plex-162")];

		await Assert.That(async () => await _testee.AppendAsync(anchorGroupId, idsToAppend)).Throws<DbUpdateException>();
	}

	private async Task SetUpGroupAsync(IEnumerable<IMediaId> ids)
	{
		var group = new MediaIdGroup();
		await fixture.Context.MediaIds.AddRangeAsync(
			ids.Select(id => new MediaIdEntry
			{
				Source = id.Source,
				ExternalId = id.ToString()!,
				Group = group,
			})
		);
		await fixture.Context.SaveChangesAsync();
	}
}
