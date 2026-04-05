using DB.Data.Entities;
using DB.Encryption;
using DB.ModelBuilders;
using Microsoft.EntityFrameworkCore;

namespace DB;

internal sealed class SammlarrDbContext : DbContext
{
	private readonly IDataProtectionService? _dataProtectionService;

	public SammlarrDbContext(DbContextOptions<SammlarrDbContext> options)
		: base(options)
	{
		Database.EnsureCreated();
	}

	public SammlarrDbContext(DbContextOptions<SammlarrDbContext> options, IDataProtectionService dataProtectionService)
		: base(options)
	{
		_dataProtectionService = dataProtectionService ?? throw new ArgumentNullException(nameof(dataProtectionService));
		Database.EnsureCreated();
	}

	public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

	public DbSet<Credential> Credentials => Set<Credential>();

	public DbSet<MediaIdGroup> MediaIdGroups => Set<MediaIdGroup>();

	public DbSet<MediaIdEntry> MediaIds => Set<MediaIdEntry>();

	public DbSet<ApiKeySetting> ApiKeySettings => Set<ApiKeySetting>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Credential>().HasIndex(c => c.AuthProvider).IsUnique();
		modelBuilder.Entity<ApiKeySetting>().HasIndex(s => s.Provider).IsUnique();
		modelBuilder.Entity<MediaIdEntry>().HasKey(e => new { e.Source, e.ExternalId });
		modelBuilder.Entity<MediaIdGroup>().HasMany(m => m.MediaIds).WithOne(e => e.Group).OnDelete(DeleteBehavior.Cascade);

		if (_dataProtectionService is not null)
		{
			var encryptedStringConverter = new EncryptedStringConverter(_dataProtectionService);
			var encryptedGuidConverter = new EncryptedGuidConverter(_dataProtectionService);
			modelBuilder.Entity<Credential>().Property(c => c.AccessToken).HasConversion(encryptedStringConverter);
			modelBuilder.Entity<Credential>().Property(c => c.ClientId).HasConversion(encryptedGuidConverter);
			modelBuilder.Entity<ApiKeySetting>().Property(a => a.ApiKey).HasConversion(encryptedStringConverter);
		}

		if (Database.ProviderName == SQLiteModelBuilder.Fullname)
		{
			SQLiteModelBuilder.OnModelCreating(modelBuilder);
		}
	}

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
