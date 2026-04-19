using Contracts.Media;
using Contracts.Media.Blacklist;
using DB.Data.Blacklist;
using DB.Data.MediaGroup;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class BlacklistRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IBlacklistRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_testee = new BlacklistRepository(fixture.Context);
	}

	[Test]
	public async Task AddAsync_WhenGroupIsNotBlacklisted_PersistsEntry()
	{
		var seedId = TmdbId.From("1001", MediaType.Movie);
		var mediaGroupId = await SeedMediaGroupAsync([seedId, ImdbId.From("tt1001", MediaType.Movie)]);

		await _testee.AddAsync(seedId);

		var storedGroupIds = await fixture.Context.BlacklistEntries.AsNoTracking().Select(x => x.Media.Id).ToListAsync();
		await Assert.That(storedGroupIds).Count().IsEqualTo(1);
		await Assert.That(storedGroupIds).Contains(mediaGroupId);
	}

	[Test]
	public async Task AddAsync_WhenGroupIsAlreadyBlacklisted_DoesNotDuplicateEntry()
	{
		var seedId = TmdbId.From("1002", MediaType.Movie);
		await SeedBlacklistedGroupAsync([seedId, ImdbId.From("tt1002", MediaType.Movie)]);

		await _testee.AddAsync(seedId);

		var count = await fixture.Context.BlacklistEntries.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task GetBlacklistedEntriesAsync_WhenEntriesExist_ReturnsMatchingProviderMediaIds()
	{
		var tmdbOne = TmdbId.From("1003", MediaType.Movie);
		var tmdbTwo = TmdbId.From("1004", MediaType.Movie);
		await SeedBlacklistedGroupAsync([tmdbOne, ImdbId.From("tt1003", MediaType.Movie)]);
		await SeedBlacklistedGroupAsync([tmdbTwo, PlexId.From("plex-1004", MediaType.Movie)]);

		var entries = await _testee.GetBlacklistedEntriesAsync(MediaProvider.Tmdb);

		await Assert.That(entries).Count().IsEqualTo(2);
		await Assert.That(entries.Select(e => e.Id)).Contains(tmdbOne);
		await Assert.That(entries.Select(e => e.Id)).Contains(tmdbTwo);
	}

	[Test]
	public async Task IsBlacklistedAsync_WhenEntryExists_ReturnsTrue()
	{
		var seedId = TmdbId.From("1005", MediaType.Movie);
		await SeedBlacklistedGroupAsync([seedId, ImdbId.From("tt1005", MediaType.Movie)]);

		var isBlacklisted = await _testee.IsBlacklistedAsync(seedId);

		await Assert.That(isBlacklisted).IsTrue();
	}

	[Test]
	public async Task AddAsync_WhenMediaIdIsNotMapped_ThrowsInvalidOperationException()
	{
		var unmappedId = TmdbId.From("9999", MediaType.Movie);

		await Assert.That(async () => await _testee.AddAsync(unmappedId)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task RemoveAsync_WhenEntryExists_RemovesEntry()
	{
		var seedId = TmdbId.From("1006", MediaType.Movie);
		var mediaGroupId = await SeedBlacklistedGroupAsync([seedId, ImdbId.From("tt1006", MediaType.Movie)]);

		await _testee.RemoveAsync(seedId);

		var exists = await fixture.Context.BlacklistEntries.AnyAsync(x => x.Media.Id == mediaGroupId);
		await Assert.That(exists).IsFalse();
	}

	[Test]
	public async Task RemoveAsync_WhenEntryDoesNotExist_DoesNotChangeData()
	{
		var existingId = TmdbId.From("1007", MediaType.Movie);
		var mediaGroupId = await SeedBlacklistedGroupAsync([existingId, ImdbId.From("tt1007", MediaType.Movie)]);
		var missingId = TmdbId.From("2007", MediaType.Movie);
		await SeedMediaGroupAsync([missingId, ImdbId.From("tt2007", MediaType.Movie)]);

		await _testee.RemoveAsync(missingId);

		var entries = await fixture.Context.BlacklistEntries.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Media.Id).IsEqualTo(mediaGroupId);
	}

	private async Task<Guid> SeedBlacklistedGroupAsync(IEnumerable<IMediaId> ids)
	{
		var mediaGroupId = await SeedMediaGroupAsync(ids);
		var mediaGroup = await fixture.Context.MediaIdGroups.FindAsync(mediaGroupId);
		fixture.Context.BlacklistEntries.Add(new BlacklistEntryEntity { Media = mediaGroup! });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return mediaGroupId;
	}

	private async Task<Guid> SeedMediaGroupAsync(IEnumerable<IMediaId> ids)
	{
		var group = new MediaIdentifierGroupEntity();
		await fixture.Context.MediaIdEntries.AddRangeAsync(
			ids.Select(id => new MediaIdentifierGroupEntry { Identifier = MediaIdentifier.From(id), MediaIdGroupEntity = group })
		);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return group.Id;
	}
}
