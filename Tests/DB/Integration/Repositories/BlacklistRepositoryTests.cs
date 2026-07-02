using Contracts.Blacklist;
using Contracts.Media;
using DB.Data.Blacklist;
using DB.Data.Media;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class BlacklistRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IBlacklistRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup() => _testee = new BlacklistRepository(fixture.ContextFactory);

	[Test]
	public async Task AddAsync_WhenGroupIsNotBlacklisted_PersistsEntry()
	{
		var mediaId = await SeedMediaAsync([TmdbId.From("1001", MediaType.Movie), ImdbId.From("tt1001", MediaType.Movie)]);

		await _testee.AddAsync(mediaId);

		var storedMediaIds = await fixture.Context.Blacklist.AsNoTracking().Select(b => b.Media.Id).ToListAsync();
		await Assert.That(storedMediaIds).Count().IsEqualTo(1);
		await Assert.That(storedMediaIds).Contains(mediaId.Value);
	}

	[Test]
	public async Task AddAsync_WhenGroupIsAlreadyBlacklisted_DoesNotDuplicateEntry()
	{
		var mediaId = await SeedBlacklistedMediaAsync([TmdbId.From("1002", MediaType.Movie), ImdbId.From("tt1002", MediaType.Movie)]);

		await _testee.AddAsync(mediaId);

		var count = await fixture.Context.Blacklist.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task GetBlacklistedEntriesAsync_WhenEntriesExist_ReturnsAllMediaIds()
	{
		var firstId = await SeedBlacklistedMediaAsync([TmdbId.From("1003", MediaType.Movie), ImdbId.From("tt1003", MediaType.Movie)]);
		var secondId = await SeedBlacklistedMediaAsync([TmdbId.From("1004", MediaType.Movie), PlexId.From("plex-1004", MediaType.Movie)]);

		var entries = await _testee.GetBlacklistedEntriesAsync();

		await Assert.That(entries).Count().IsEqualTo(2);
		await Assert.That(entries.Select(e => e.MediaId)).Contains(firstId);
		await Assert.That(entries.Select(e => e.MediaId)).Contains(secondId);
	}

	[Test]
	public async Task IsBlacklistedAsync_WhenEntryExists_ReturnsTrue()
	{
		var mediaId = await SeedBlacklistedMediaAsync([TmdbId.From("1005", MediaType.Movie), ImdbId.From("tt1005", MediaType.Movie)]);

		var isBlacklisted = await _testee.IsBlacklistedAsync(mediaId);

		await Assert.That(isBlacklisted).IsTrue();
	}

	[Test]
	public async Task AddAsync_WhenMediaIdIsNotMapped_ThrowsInvalidOperationException()
	{
		var unmappedId = MediaId.From(Guid.NewGuid());

		await Assert.That(async () => await _testee.AddAsync(unmappedId)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task RemoveAsync_WhenEntryExists_RemovesEntry()
	{
		var mediaId = await SeedBlacklistedMediaAsync([TmdbId.From("1006", MediaType.Movie), ImdbId.From("tt1006", MediaType.Movie)]);

		await _testee.RemoveAsync(mediaId);

		var exists = await fixture.Context.Blacklist.AnyAsync(b => b.Media.Id == mediaId.Value);
		await Assert.That(exists).IsFalse();
	}

	[Test]
	public async Task RemoveAsync_WhenEntryDoesNotExist_DoesNotChangeData()
	{
		var existingId = await SeedBlacklistedMediaAsync([TmdbId.From("1007", MediaType.Movie), ImdbId.From("tt1007", MediaType.Movie)]);
		var missingId = await SeedMediaAsync([TmdbId.From("2007", MediaType.Movie), ImdbId.From("tt2007", MediaType.Movie)]);

		await _testee.RemoveAsync(missingId);

		var entries = await fixture.Context.Blacklist.AsNoTracking().Include(b => b.Media).ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Media.Id).IsEqualTo(existingId.Value);
	}

	private async Task<MediaId> SeedBlacklistedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var mediaId = await SeedMediaAsync(ids);
		var media = await fixture.Context.Media.FindAsync(mediaId.Value);
		fixture.Context.Blacklist.Add(new BlacklistRow { Media = media! });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return mediaId;
	}

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}
}
