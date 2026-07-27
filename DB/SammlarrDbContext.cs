using System.Reflection;
using Contracts;
using Contracts.Identity;
using DB.Data;
using DB.Data.Access;
using DB.Data.Blacklist;
using DB.Data.Config;
using DB.Data.Feed;
using DB.Data.Identity;
using DB.Data.Media;
using DB.Data.Metadata;
using DB.Data.Watchlist;
using DB.Data.Watchlist.Outbox;
using Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB;

internal sealed class SammlarrDbContext : DbContext
{
	private static readonly MethodInfo SetUserFilterMethod = typeof(SammlarrDbContext).GetMethod(
		nameof(SetUserFilter),
		BindingFlags.Instance | BindingFlags.NonPublic
	)!;

	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly EntityAuditer _entityAuditer;
	private readonly UserOwnershipStamper _userOwnershipStamper;
	private readonly ICurrentUser _currentUser;
	private readonly IEnumerable<IPropertyAddedConvention> _propertyConventions;
	private readonly IEnumerable<IModelConfiguration> _modelConfigurations;

	public SammlarrDbContext(
		DbContextOptions<SammlarrDbContext> options,
		EntityAuditer entityAuditer,
		UserOwnershipStamper userOwnershipStamper,
		ICurrentUser currentUser,
		IEnumerable<IPropertyAddedConvention> propertyConventions,
		IEnumerable<IModelConfiguration> modelConfigurations,
		IDateTimeProvider dateTimeProvider
	)
		: base(options)
	{
		_propertyConventions = propertyConventions;
		_modelConfigurations = modelConfigurations;
		_entityAuditer = entityAuditer;
		_userOwnershipStamper = userOwnershipStamper;
		_currentUser = currentUser;
		_dateTimeProvider = dateTimeProvider;
	}

	private Guid CurrentUserId => _currentUser.IsAuthenticated ? _currentUser.Id.Value : Guid.Empty;

	public DbSet<CredentialRow> Credentials => Set<CredentialRow>();

	public DbSet<MediaRow> Media => Set<MediaRow>();

	public DbSet<ExternalMediaIdRow> ExternalMediaIds => Set<ExternalMediaIdRow>();

	public DbSet<MediaMetadataRow> MediaMetadata => Set<MediaMetadataRow>();

	public DbSet<MediaRatingRow> MediaRatings => Set<MediaRatingRow>();

	public DbSet<ApiKeyConfigRow> ApiKeySettings => Set<ApiKeyConfigRow>();

	public DbSet<ProviderConfigRow> ProviderSettings => Set<ProviderConfigRow>();
	public DbSet<FeedRow> Feeds => Set<FeedRow>();
	public DbSet<BlacklistRow> Blacklist => Set<BlacklistRow>();
	public DbSet<WatchlistRow> Watchlist => Set<WatchlistRow>();
	public DbSet<WatchlistOutboxRow> WatchlistOutbox => Set<WatchlistOutboxRow>();
	public DbSet<UserRow> Users => Set<UserRow>();

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

		modelBuilder
			.Model.GetEntityTypes()
			.Where(entityType => typeof(IUserOwned).IsAssignableFrom(entityType.ClrType))
			.ToList()
			.ForEach(entityType => SetUserFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]));
	}

	private void SetUserFilter<TEntity>(ModelBuilder modelBuilder)
		where TEntity : class =>
		modelBuilder.Entity<TEntity>().HasQueryFilter(entity => EF.Property<Guid>(entity, UserOwnedConfigurationExtensions.UserIdColumn) == CurrentUserId);

	public override int SaveChanges()
	{
		ApplyStamps();
		return base.SaveChanges();
	}

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		ApplyStamps();
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		ApplyStamps();
		return base.SaveChangesAsync(cancellationToken);
	}

	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		ApplyStamps();
		return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
	}

	private void ApplyStamps()
	{
		var now = _dateTimeProvider.Now;
		ChangeTracker.Entries<IAuditable>().ToList().ForEach(entry => _entityAuditer.UpdateTimestamps(entry, now));
		ChangeTracker.Entries<IUserOwned>().ToList().ForEach(entry => _userOwnershipStamper.Stamp(entry, CurrentUserId));
	}
}
