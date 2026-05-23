using Contracts.Media;
using Contracts.Media.Blacklist;
using DB.Data.Blacklist;
using DB.Data.Media;
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
		var seedId = TmdbId.From("1001", MediaType.Movie);
		var mediaId = await SeedMediaAsync([seedId, ImdbId.From("tt1001", MediaType.Movie)]);

		await _testee.AddAsync(seedId);

		var storedMediaIds = await fixture.Context.Blacklist.AsNoTracking().Select(b => b.Media.Id).ToListAsync();
		await Assert.That(storedMediaIds).Count().IsEqualTo(1);
		await Assert.That(storedMediaIds).Contains(mediaId);
	}

	[Test]
	public async Task AddAsync_WhenGroupIsAlreadyBlacklisted_DoesNotDuplicateEntry()
	{
		var seedId = TmdbId.From("1002", MediaType.Movie);
		await SeedBlacklistedMediaAsync([seedId, ImdbId.From("tt1002", MediaType.Movie)]);

		await _testee.AddAsync(seedId);

		var count = await fixture.Context.Blacklist.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task GetBlacklistedEntriesAsync_WhenEntriesExist_ReturnsMatchingProviderMediaIds()
	{
		var tmdbOne = TmdbId.From("1003", MediaType.Movie);
		var tmdbTwo = TmdbId.From("1004", MediaType.Movie);
		await SeedBlacklistedMediaAsync([tmdbOne, ImdbId.From("tt1003", MediaType.Movie)]);
		await SeedBlacklistedMediaAsync([tmdbTwo, PlexId.From("plex-1004", MediaType.Movie)]);

		var entries = await _testee.GetBlacklistedEntriesAsync(MediaProvider.Tmdb);

		await Assert.That(entries).Count().IsEqualTo(2);
		await Assert.That(entries.Select(e => e.MediaId)).Contains(tmdbOne);
		await Assert.That(entries.Select(e => e.MediaId)).Contains(tmdbTwo);
	}

	[Test]
	public async Task IsBlacklistedAsync_WhenEntryExists_ReturnsTrue()
	{
		var seedId = TmdbId.From("1005", MediaType.Movie);
		await SeedBlacklistedMediaAsync([seedId, ImdbId.From("tt1005", MediaType.Movie)]);

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
		var mediaId = await SeedBlacklistedMediaAsync([seedId, ImdbId.From("tt1006", MediaType.Movie)]);

		await _testee.RemoveAsync(seedId);

		var exists = await fixture.Context.Blacklist.AnyAsync(b => b.Media.Id == mediaId);
		await Assert.That(exists).IsFalse();
	}

	[Test]
	public async Task RemoveAsync_WhenEntryDoesNotExist_DoesNotChangeData()
	{
		var existingId = TmdbId.From("1007", MediaType.Movie);
		var mediaId = await SeedBlacklistedMediaAsync([existingId, ImdbId.From("tt1007", MediaType.Movie)]);
		var missingId = TmdbId.From("2007", MediaType.Movie);
		await SeedMediaAsync([missingId, ImdbId.From("tt2007", MediaType.Movie)]);

		await _testee.RemoveAsync(missingId);

		var entries = await fixture.Context.Blacklist.AsNoTracking().Include(b => b.Media).ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Media.Id).IsEqualTo(mediaId);
	}

	private async Task<Guid> SeedBlacklistedMediaAsync(IEnumerable<IMediaId> ids)
	{
		var mediaId = await SeedMediaAsync(ids);
		var media = await fixture.Context.Media.FindAsync(mediaId);
		fixture.Context.Blacklist.Add(new BlacklistEntity { Media = media! });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return mediaId;
	}

	private async Task<Guid> SeedMediaAsync(IEnumerable<IMediaId> ids)
	{
		var media = new MediaEntity();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalId(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return media.Id;
	}
}
