using Contracts;
using DB.Data;
using DB.Data.Access;
using DB.Data.Blacklist;
using DB.Data.Config;
using DB.Data.Feed;
using DB.Data.Media;
using DB.Data.Metadata;
using DB.Data.Watchlist;
using DB.Data.Watchlist.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB;

internal sealed class SammlarrDbContext : DbContext
{
	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly EntityAuditer _entityAuditer;
	private readonly IEnumerable<IPropertyAddedConvention> _propertyConventions;
	private readonly IEnumerable<IModelConfiguration> _modelConfigurations;

	public SammlarrDbContext(
		DbContextOptions<SammlarrDbContext> options,
		EntityAuditer entityAuditer,
		IEnumerable<IPropertyAddedConvention> propertyConventions,
		IEnumerable<IModelConfiguration> modelConfigurations,
		IDateTimeProvider dateTimeProvider
	)
		: base(options)
	{
		_propertyConventions = propertyConventions;
		_modelConfigurations = modelConfigurations;
		_entityAuditer = entityAuditer;
		_dateTimeProvider = dateTimeProvider;
	}

	public void Initialize()
	{
		Database.EnsureCreated();
		SeedProviderSettings();
	}

	public DbSet<CredentialRow> Credentials => Set<CredentialRow>();

	public DbSet<MediaRow> Media => Set<MediaRow>();

	public DbSet<ExternalMediaIdRow> ExternalMediaIds => Set<ExternalMediaIdRow>();

	public DbSet<MediaMetadataRow> MediaMetadata => Set<MediaMetadataRow>();

	public DbSet<MediaRatingRow> MediaRatings => Set<MediaRatingRow>();

	public DbSet<ApiKeyConfigRow> ApiKeySettings => Set<ApiKeyConfigRow>();

	public DbSet<ProviderConfigEntity> ProviderSettings => Set<ProviderConfigEntity>();
	public DbSet<FeedEntity> Feeds => Set<FeedEntity>();
	public DbSet<BlacklistEntity> Blacklist => Set<BlacklistEntity>();
	public DbSet<WatchlistEntity> Watchlist => Set<WatchlistEntity>();
	public DbSet<WatchlistOutboxEntity> WatchlistOutbox => Set<WatchlistOutboxEntity>();

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
		_modelConfigurations.ToList().ForEach(builder => builder.Apply(modelBuilder));
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
