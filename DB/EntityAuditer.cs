using System.Diagnostics.CodeAnalysis;
using DB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DB;

internal sealed class EntityAuditer
{
	[SuppressMessage("Performance", "CA1822", Justification = "The auditer is an injected service, so the DB context takes it as an instance.")]
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
