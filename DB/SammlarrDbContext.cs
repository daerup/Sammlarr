using System.Reflection;
using Contracts;
using Contracts.Identity;
using DB.Data;
using DB.Data.Access;
using DB.Data.Blocklist;
using DB.Data.Config;
using DB.Data.Datasets;
using DB.Data.Feed;
using DB.Data.Feed.Run;
using DB.Data.Identity;
using DB.Data.Jobs;
using DB.Data.Media;
using DB.Data.Notifications;
using DB.Data.Ratings;
using DB.Data.Usage;
using DB.Data.Wantlist;
using DB.Data.Wantlist.Outbox;
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

	private Guid CurrentUserId =>
		_currentUser.IsAuthenticated
			? _currentUser.Id.Value
			: throw new InvalidOperationException("No authenticated user in scope; user-owned data cannot be accessed anonymously.");

	public DbSet<CredentialRow> Credentials => Set<CredentialRow>();

	public DbSet<MediaRow> Media => Set<MediaRow>();

	public DbSet<ExternalMediaIdRow> ExternalMediaIds => Set<ExternalMediaIdRow>();

	public DbSet<MediaDetailsRow> MediaDetails => Set<MediaDetailsRow>();

	public DbSet<MediaRatingRow> MediaRatings => Set<MediaRatingRow>();
	public DbSet<ProviderAnswerRow> ProviderAnswers => Set<ProviderAnswerRow>();

	public DbSet<ApiKeyConfigRow> ApiKeySettings => Set<ApiKeyConfigRow>();

	public DbSet<ProviderConfigRow> ProviderSettings => Set<ProviderConfigRow>();
	public DbSet<FeedRow> Feeds => Set<FeedRow>();
	public DbSet<FeedRunRow> FeedRuns => Set<FeedRunRow>();
	public DbSet<FeedRunItemRow> FeedRunItems => Set<FeedRunItemRow>();
	public DbSet<FeedRunCallsRow> FeedRunCalls => Set<FeedRunCallsRow>();

	public DbSet<FeedRunTimesRow> FeedRunTimes => Set<FeedRunTimesRow>();
	public DbSet<BlocklistRow> Blocklist => Set<BlocklistRow>();
	public DbSet<WantlistRow> Wantlist => Set<WantlistRow>();
	public DbSet<WantlistOutboxRow> WantlistOutbox => Set<WantlistOutboxRow>();
	public DbSet<UserRow> Users => Set<UserRow>();
	public DbSet<AccountLinkRow> AccountLinks => Set<AccountLinkRow>();
	public DbSet<JobRow> Jobs => Set<JobRow>();
	public DbSet<NotificationTargetRow> NotificationTargets => Set<NotificationTargetRow>();
	public DbSet<NotificationSubscriptionRow> NotificationSubscriptions => Set<NotificationSubscriptionRow>();
	public DbSet<NotificationDeliveryOutcomeRow> NotificationDeliveryOutcomes => Set<NotificationDeliveryOutcomeRow>();
	public DbSet<RatingDefaultRow> RatingDefaults => Set<RatingDefaultRow>();
	public DbSet<ImdbRatingRow> ImdbRatings => Set<ImdbRatingRow>();
	public DbSet<DatasetLoadRow> DatasetLoads => Set<DatasetLoadRow>();
	public DbSet<AnimeIdRow> AnimeIds => Set<AnimeIdRow>();
	public DbSet<RemoteUsageRow> RemoteUsage => Set<RemoteUsageRow>();
	public DbSet<RemoteAttemptRow> RemoteAttempts => Set<RemoteAttemptRow>();

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder
			.UseSeeding((context, _) => EnsureProviderConfig(context))
			.UseAsyncSeeding((context, _, cancellationToken) => EnsureProviderConfigAsync(context, cancellationToken));
	}

	private static void EnsureProviderConfig(DbContext context)
	{
		var settings = context.Set<ProviderConfigRow>();
		if (!settings.Any())
		{
			settings.Add(ProviderConfigRow.Default);
			context.SaveChanges();
		}
	}

	private static async Task EnsureProviderConfigAsync(DbContext context, CancellationToken cancellationToken)
	{
		var settings = context.Set<ProviderConfigRow>();
		if (!await settings.AnyAsync(cancellationToken))
		{
			settings.Add(ProviderConfigRow.Default);
			await context.SaveChangesAsync(cancellationToken);
		}
	}

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
