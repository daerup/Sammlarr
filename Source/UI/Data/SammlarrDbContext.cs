using Microsoft.EntityFrameworkCore;
using UI.Data.Entities;

namespace UI.Data;

public sealed class SammlarrDbContext : DbContext
{
	public SammlarrDbContext(DbContextOptions<SammlarrDbContext> options)
		: base(options) => Database.EnsureCreated();

	public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

	public DbSet<PlexCredential> PlexCredentials => Set<PlexCredential>();

	public override int SaveChanges()
	{
		ApplyAuditTimestamps();
		return base.SaveChanges();
	}

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		ApplyAuditTimestamps();
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		ApplyAuditTimestamps();
		return base.SaveChangesAsync(cancellationToken);
	}

	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		ApplyAuditTimestamps();
		return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
	}

	private void ApplyAuditTimestamps()
	{
		var now = DateTime.UtcNow;
		foreach (var entry in ChangeTracker.Entries<IAuditable>())
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
				entry.Entity.CreatedAt = now;
				entry.Property(a => a.CreatedAt).IsModified = true;
			}

			if (setUpdatedAt)
			{
				entry.Entity.UpdatedAt = now;
				entry.Property(a => a.UpdatedAt).IsModified = true;
			}
		}
	}
}
