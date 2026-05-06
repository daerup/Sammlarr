using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts;
using Contracts.Media;
using Contracts.Media.Feed;
using Contracts.Processing;
using DB.Data;
using DB.Data.Access;
using DB.Data.Blacklist;
using DB.Data.Config;
using DB.Data.Feed;
using DB.Data.MediaGroup;
using DB.Data.Metadata;
using DB.Data.Watchlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB;

internal sealed class SammlarrDbContext : DbContext
{
	private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly EntityAuditer _entityAuditer;
	private readonly IEnumerable<IPropertyAddedConvention> _propertyConventions;

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

	public DbSet<MediaMetadataEntity> MediaMetadata => Set<MediaMetadataEntity>();

	public DbSet<MediaRatingEntity> MediaRatings => Set<MediaRatingEntity>();

	public DbSet<ApiKeyConfigEntity> ApiKeySettings => Set<ApiKeyConfigEntity>();

	public DbSet<ProviderConfigEntity> ProviderSettings => Set<ProviderConfigEntity>();
	public DbSet<FeedEntity> Feeds => Set<FeedEntity>();
	public DbSet<BlacklistEntryEntity> BlacklistEntries => Set<BlacklistEntryEntity>();

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
		modelBuilder.Entity<BlacklistEntryEntity>().HasOne(x => x.Media).WithMany().HasForeignKey("MediaId");
		modelBuilder.Entity<BlacklistEntryEntity>().HasIndex("MediaId").IsUnique();

		modelBuilder.Entity<MediaIdentifierGroupEntry>(builder =>
		{
			builder.Property(e => e.Identifier).HasMaxLength(160).HasConversion<MediaIdentifierConverter>();
			builder.HasKey(e => e.Identifier);
		});
		modelBuilder.Entity<MediaIdentifierGroupEntity>().HasMany(m => m.MediaIdEntries).WithOne(e => e.MediaIdGroupEntity).OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<MediaMetadataEntity>(builder =>
		{
			builder.HasOne(m => m.MediaIdGroup).WithOne().HasForeignKey<MediaMetadataEntity>(m => m.MediaIdGroupId).OnDelete(DeleteBehavior.Cascade);
			builder.HasDiscriminator(m => m.MediaType).HasValue<MovieMetadataEntity>(MediaType.Movie).HasValue<ShowMetadataEntity>(MediaType.Show);
		});

		modelBuilder.Entity<MediaRatingEntity>(builder =>
		{
			builder.Property<Guid>("MediaIdGroupId");
			builder.HasKey("MediaIdGroupId", nameof(MediaRatingEntity.Source));
			builder.HasOne(r => r.Metadata).WithMany(m => m.Ratings).HasForeignKey("MediaIdGroupId").OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<FeedEntity>(builder =>
		{
			builder
				.Property(x => x.Filter)
				.HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QueryFilter>(v, JsonOptions)!);
			builder
				.Property(x => x.Sort)
				.HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QuerySort>(v, JsonOptions)!);
			builder
				.Property(x => x.Limit)
				.HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QueryLimit>(v, JsonOptions)!);
			builder.Property(x => x.SyncMode).HasConversion(v => v.ToString(), v => SyncMode.From(v));
		});
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
