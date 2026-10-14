using DB;
using DB.Data;
using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class EntityAuditerTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private readonly DateTimeOffset _now = DateTimeOffset.Now;
	private readonly DateTimeOffset _then = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(10));

	[Test]
	public async Task UpdateTimestamps_WhenEntityIsAdded_SetsCreatedAtAndUpdatedAt()
	{
		var entry = GetAuditableEntry(EntityState.Added);

		EntityAuditer.UpdateTimestamps(entry, _now);

		await Assert.That(entry.Entity.CreatedAt).IsEqualTo(_now);
		await Assert.That(entry.Entity.UpdatedAt).IsEqualTo(_now);
	}

	[Test]
	public async Task UpdateTimestamps_WhenEntityIsModified_SetsOnlyUpdatedAt()
	{
		var entry = GetAuditableEntry(EntityState.Modified);

		EntityAuditer.UpdateTimestamps(entry, _now);

		await Assert.That(entry.Entity.CreatedAt).IsEqualTo(_then);
		await Assert.That(entry.Entity.UpdatedAt).IsEqualTo(_now);
	}

	[Test]
	[Arguments(EntityState.Deleted)]
	[Arguments(EntityState.Unchanged)]
	public async Task UpdateTimestamps_WhenEntityIsDeletedOrUnchanged_DoesNotModifyTimestamps(EntityState state)
	{
		var entry = GetAuditableEntry(state);

		EntityAuditer.UpdateTimestamps(entry, _now);

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

		var entry = fixture.Context.Entry<IAuditable>(entity);
		entry.State = state;

		return entry;
	}
}
