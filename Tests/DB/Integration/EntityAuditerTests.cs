using Contracts.Media;
using DB.Data;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DB.Tests.Integration;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class EntityAuditerTests(SQLiteFixture fixture)
{
	private readonly DateTimeOffset _now = DateTimeOffset.Now;
	private readonly EntityAuditer _testee = new();
	private readonly DateTimeOffset _then = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(10));

	[Test]
	public async Task UpdateTimestamps_WhenEntityIsAdded_SetsCreatedAtAndUpdatedAt()
	{
		var entry = GetAuditableEntry(EntityState.Added);

		_testee.UpdateTimestamps(entry, _now);

		await Assert.That(entry.Entity.CreatedAt).IsEqualTo(_now);
		await Assert.That(entry.Entity.UpdatedAt).IsEqualTo(_now);
	}

	[Test]
	public async Task UpdateTimestamps_WhenEntityIsModified_SetsOnlyUpdatedAt()
	{
		var entry = GetAuditableEntry(EntityState.Modified);

		_testee.UpdateTimestamps(entry, _now);

		await Assert.That(entry.Entity.CreatedAt).IsEqualTo(_then);
		await Assert.That(entry.Entity.UpdatedAt).IsEqualTo(_now);
	}

	[Test]
	[Arguments(EntityState.Deleted)]
	[Arguments(EntityState.Unchanged)]
	[Arguments(EntityState.Detached)]
	public async Task UpdateTimestamps_WhenEntityIsUnchanged_DoesNotModifyTimestamps(EntityState state)
	{
		var entry = GetAuditableEntry(EntityState.Unchanged);

		_testee.UpdateTimestamps(entry, _now);

		await Assert.That(entry.Entity.CreatedAt).IsEqualTo(_then);
		await Assert.That(entry.Entity.UpdatedAt).IsEqualTo(_then);
	}

	private EntityEntry<IAuditable> GetAuditableEntry(EntityState state)
	{
		var entity = new MediaRow
		{
			Id = Guid.NewGuid(),
			UpdatedAt = _then,
			CreatedAt = _then,
		};
		entity.ExternalIds.Add(
			new ExternalMediaIdRow
			{
				Source = MediaProvider.Tmdb,
				MediaType = MediaType.Movie,
				Id = "12345",
				Media = entity,
			}
		);

		fixture.Context.Add(entity);
		var entry = fixture.Context.Entry<IAuditable>(entity);
		entry.State = state;

		return entry;
	}
}
