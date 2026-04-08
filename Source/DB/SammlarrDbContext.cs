using Contracts;
using DB.Data.Entities;
using DB.Encryption;
using DB.ModelBuilders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB;

internal sealed class SammlarrDbContext : DbContext
{
	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly IDataProtectionService _dataProtectionService;
	private readonly EntityAuditer _entityAuditer;

	public SammlarrDbContext(
		DbContextOptions<SammlarrDbContext> options,
		EntityAuditer entityAuditer,
		IDataProtectionService dataProtectionService,
		IDateTimeProvider dateTimeProvider
	)
		: base(options)
	{
		_dataProtectionService = dataProtectionService;
		_entityAuditer = entityAuditer;
		_dateTimeProvider = dateTimeProvider;
		Database.EnsureCreated();
	}

	public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

	public DbSet<Credential> Credentials => Set<Credential>();

	public DbSet<MediaIdGroup> MediaIdGroups => Set<MediaIdGroup>();

	public DbSet<MediaIdEntry> MediaIds => Set<MediaIdEntry>();

	public DbSet<ApiKeySetting> ApiKeySettings => Set<ApiKeySetting>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.Conventions.Add(_ => new EncryptedPropertyConvention(_dataProtectionService));

		if (Database.ProviderName == SQLiteModelBuilder.Fullname)
		{
			configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
			configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
		}
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Credential>().HasIndex(c => c.AuthProvider).IsUnique();
		modelBuilder.Entity<ApiKeySetting>().HasIndex(s => s.Provider).IsUnique();
		modelBuilder.Entity<MediaIdEntry>().HasKey(e => new { e.Source, e.ExternalId });
		modelBuilder.Entity<MediaIdGroup>().HasMany(m => m.MediaIds).WithOne(e => e.Group).OnDelete(DeleteBehavior.Cascade);
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
		var now = _dateTimeProvider.Now;
		ChangeTracker.Entries<IAuditable>().ToList().ForEach(entry => _entityAuditer.UpdateTimestamps(entry, now));
	}
}
