using Contracts.Media.List;
using DB.Data.List;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class ImportListRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IImportListRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_testee = new ImportListRepository(fixture.Context);
	}

	[Test]
	public async Task GetImportListsAsync_WhenListsExist_ReturnsNewestFirst()
	{
		var oldestId = await SeedListAsync("Oldest", "oldest", new DateTimeOffset(2026, 01, 01, 8, 0, 0, TimeSpan.Zero));
		var middleId = await SeedListAsync("Middle", "middle", new DateTimeOffset(2026, 02, 01, 8, 0, 0, TimeSpan.Zero));
		var newestId = await SeedListAsync("Newest", "newest", new DateTimeOffset(2026, 03, 01, 8, 0, 0, TimeSpan.Zero));

		var lists = await _testee.GetImportListsAsync();

		await Assert.That(lists).Count().IsEqualTo(3);
		await Assert.That(lists[0].Id).IsEqualTo(newestId);
		await Assert.That(lists[1].Id).IsEqualTo(middleId);
		await Assert.That(lists[2].Id).IsEqualTo(oldestId);
	}

	[Test]
	public async Task GetImportListsAsync_WhenListExists_MapsCreatedAt()
	{
		var expectedCreatedAt = new DateTimeOffset(2026, 04, 01, 10, 30, 0, TimeSpan.Zero);
		var id = await SeedListAsync("CreatedAt Check", "created-at-check", expectedCreatedAt);

		var list = (await _testee.GetImportListsAsync()).Single(l => l.Id == id);

		await Assert.That(list.CreatedAt).IsEqualTo(expectedCreatedAt);
	}

	[Test]
	public async Task DeleteImportListAsync_WhenListExists_RemovesList()
	{
		var id = await SeedListAsync("To Delete", "to-delete", new DateTimeOffset(2026, 05, 01, 12, 0, 0, TimeSpan.Zero));

		await _testee.DeleteImportListAsync(id);

		var exists = await fixture.Context.Lists.AnyAsync(l => l.Id == id);
		await Assert.That(exists).IsFalse();
	}

	[Test]
	public async Task DeleteImportListAsync_WhenListDoesNotExist_DoesNotChangeData()
	{
		var id = await SeedListAsync("Keep", "keep", new DateTimeOffset(2026, 06, 01, 12, 0, 0, TimeSpan.Zero));

		await _testee.DeleteImportListAsync(Guid.NewGuid());

		var lists = await fixture.Context.Lists.AsNoTracking().ToListAsync();
		await Assert.That(lists).Count().IsEqualTo(1);
		await Assert.That(lists[0].Id).IsEqualTo(id);
	}

	private async Task<Guid> SeedListAsync(string name, string identifier, DateTimeOffset createdAt)
	{
		var entity = new ImportListEntity
		{
			Name = name,
			IsEnabled = true,
			Provider = MediaListProvider.Mdblist,
			Strategy = MediaListAddStrategy.Append,
			Identifier = identifier,
		};

		fixture.Context.Lists.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return entity.Id;
	}
}
