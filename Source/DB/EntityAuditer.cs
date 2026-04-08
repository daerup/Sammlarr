using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DB;

internal class EntityAuditer
{
	public void UpdateTimestamps(EntityEntry<IAuditable> entry, DateTimeOffset timestamp)
	{
		entry.Property(a => a.CreatedAt).IsModified = false;
		entry.Property(a => a.UpdatedAt).IsModified = false;

		var (setCreatedAt, setUpdatedAt) = entry.State switch
		{
			EntityState.Added => (true, true),
			EntityState.Modified => (false, true),
			_ => (false, false),
		};

		if (setCreatedAt)
		{
			entry.Entity.CreatedAt = timestamp;
			entry.Property(a => a.CreatedAt).IsModified = true;
		}

		if (setUpdatedAt)
		{
			entry.Entity.UpdatedAt = timestamp;
			entry.Property(a => a.UpdatedAt).IsModified = true;
		}
	}
}
