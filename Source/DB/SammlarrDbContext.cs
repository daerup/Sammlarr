using Contracts;
using DB.Data;
using DB.Data.Access;
using DB.Data.Config;
using DB.Data.MediaGroup;
using DB.Data.Watchlist;
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

	public DbSet<CollectionItemEntity> CollectionItems => Set<CollectionItemEntity>();

	public DbSet<CredentialEntity> Credentials => Set<CredentialEntity>();

	public DbSet<MediaIdentifierGroupEntity> MediaIdGroups => Set<MediaIdentifierGroupEntity>();

	public DbSet<MediaIdentifierGroupEntry> MediaIdEntries => Set<MediaIdentifierGroupEntry>();

	public DbSet<ApiKeyConfigEntity> ApiKeySettings => Set<ApiKeyConfigEntity>();

	public DbSet<ProviderConfigEntity> ProviderSettings => Set<ProviderConfigEntity>();

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

		modelBuilder.Entity<CredentialEntity>().HasIndex(c => c.WatchlistProvider).IsUnique();
		modelBuilder.Entity<ApiKeyConfigEntity>().HasIndex(s => s.Provider).IsUnique();
		modelBuilder
			.Entity<ProviderConfigEntity>()
			.ToTable(
				"ProviderSettings",
				tableBuilder =>
				{
					tableBuilder.HasCheckConstraint("CK_ProviderSettings_Singleton", $"Id = '{ProviderConfigEntity.SingletonId}'");
				}
			);

		modelBuilder.Entity<MediaIdentifierGroupEntry>().Property(e => e.Identifier).HasMaxLength(160).HasConversion<MediaIdentifierConverter>();
		modelBuilder.Entity<MediaIdentifierGroupEntry>().HasKey(e => e.Identifier);
		modelBuilder.Entity<MediaIdentifierGroupEntity>().HasMany(m => m.MediaIdEntries).WithOne(e => e.MediaIdGroupEntity).OnDelete(DeleteBehavior.Cascade);
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

		ProviderSettings.Add(ProviderConfigEntity.Default);
		base.SaveChanges();
	}
}
