using Contracts;
using DB.Data;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB;

internal sealed class SammlarrDbContext : DbContext
{
	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly IEnumerable<IPropertyAddedConvention> _propertyConventions;
	private readonly EntityAuditer _entityAuditer;

	public SammlarrDbContext(
		DbContextOptions<SammlarrDbContext> options,
		EntityAuditer entityAuditer,
		IEnumerable<IPropertyAddedConvention> propertyConventions,
		IDateTimeProvider dateTimeProvider
	)
		: base(options)
	{
		_propertyConventions = propertyConventions;
		_entityAuditer = entityAuditer;
		_dateTimeProvider = dateTimeProvider;
		Database.EnsureCreated();
		SeedProviderSettings();
	}

	public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

	public DbSet<Credential> Credentials => Set<Credential>();

	public DbSet<MediaIdentifierGroup> MediaIdGroups => Set<MediaIdentifierGroup>();

	public DbSet<MediaIdentifierEntry> MediaIdEntries => Set<MediaIdentifierEntry>();

	public DbSet<ApiKeySetting> ApiKeySettings => Set<ApiKeySetting>();

	public DbSet<ProviderSettingsEntity> ProviderSettings => Set<ProviderSettingsEntity>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		_propertyConventions.ToList().ForEach(convention => configurationBuilder.Conventions.Add(_ => convention));

		if (Database.IsSqlite())
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
		modelBuilder
			.Entity<ProviderSettingsEntity>()
			.ToTable(
				"ProviderSettings",
				tableBuilder =>
				{
					tableBuilder.HasCheckConstraint("CK_ProviderSettings_Singleton", "Id = '00000000-0000-0000-0000-000000000001'");
				}
			);
		modelBuilder.Entity<MediaIdentifierEntry>().Property(e => e.Identifier).HasMaxLength(160).HasConversion<MediaIdentifierConverter>();
		modelBuilder.Entity<MediaIdentifierEntry>().HasKey(e => e.Identifier);
		modelBuilder.Entity<MediaIdentifierGroup>().HasMany(m => m.MediaIdEntries).WithOne(e => e.MediaIdGroup).OnDelete(DeleteBehavior.Cascade);
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

	private void SeedProviderSettings()
	{
		if (ProviderSettings.Any())
		{
			return;
		}

		ProviderSettings.Add(DB.Data.Entities.ProviderSettingsEntity.Default);
		base.SaveChanges();
	}
}
