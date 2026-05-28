using DB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DB;

internal class EntityAuditer
{
	public void UpdateTimestamps(EntityEntry<IAuditable> entry, DateTimeOffset timestamp)
	{
		if (entry.State is EntityState.Added)
		{
			entry.Entity.CreatedAt = timestamp;
		}
		else
		{
			entry.Property(a => a.CreatedAt).IsModified = false;
		}

		if (entry.State is EntityState.Added or EntityState.Modified)
		{
			entry.Entity.UpdatedAt = timestamp;
		}
		else
		{
			entry.Property(a => a.UpdatedAt).IsModified = false;
		}
	}
}
