using System.Reflection;
using Contracts;
using Contracts.Identity;
using DB.Data;
using DB.Data.Auth;
using DB.Data.Blocklist;
using DB.Data.Config;
using DB.Data.Datasets;
using DB.Data.Feed;
using DB.Data.Feed.Run;
using DB.Data.Housekeeping;
using DB.Data.Identity;
using DB.Data.Jobs;
using DB.Data.Media;
using DB.Data.Notifications;
using DB.Data.Ratings;
using DB.Data.Usage;
using DB.Data.Wantlist;
using DB.Data.Wantlist.Outbox;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB;

internal sealed class SammlarrDbContext(
	DbContextOptions<SammlarrDbContext> options,
	EntityAuditer entityAuditer,
	UserOwnershipStamper userOwnershipStamper,
	ICurrentUser currentUser,
	IEnumerable<IPropertyAddedConvention> propertyConventions,
	IEnumerable<IModelConfiguration> modelConfigurations,
	IDateTimeProvider dateTimeProvider
) : IdentityDbContext<UserRow, IdentityRole<Guid>, Guid>(options)
{
	private static readonly MethodInfo SetUserFilterMethod = typeof(SammlarrDbContext).GetMethod(
		nameof(SetUserFilter),
		BindingFlags.Instance | BindingFlags.NonPublic
	)!;

	private Guid CurrentUserId =>
		currentUser.IsAuthenticated
			? currentUser.Id.Value
			: throw new InvalidOperationException("No authenticated user in scope; user-owned data cannot be accessed anonymously.");

	public DbSet<CredentialRow> Credentials => Set<CredentialRow>();

	public DbSet<MediaRow> Media => Set<MediaRow>();

	public DbSet<ExternalMediaIdRow> ExternalMediaIds => Set<ExternalMediaIdRow>();

	public DbSet<ShowPartRow> ShowParts => Set<ShowPartRow>();

	public DbSet<MediaDetailsRow> MediaDetails => Set<MediaDetailsRow>();

	public DbSet<MediaRatingRow> MediaRatings => Set<MediaRatingRow>();
	public DbSet<ProviderAnswerRow> ProviderAnswers => Set<ProviderAnswerRow>();

	public DbSet<ApiKeyConfigRow> ApiKeySettings => Set<ApiKeyConfigRow>();

	public DbSet<ProviderConfigRow> ProviderSettings => Set<ProviderConfigRow>();
	public DbSet<FeedRow> Feeds => Set<FeedRow>();
	public DbSet<FeedRunRow> FeedRuns => Set<FeedRunRow>();
	public DbSet<FeedAbsenceRow> FeedAbsences => Set<FeedAbsenceRow>();
	public DbSet<FeedRunItemRow> FeedRunItems => Set<FeedRunItemRow>();
	public DbSet<FeedRunCallsRow> FeedRunCalls => Set<FeedRunCallsRow>();

	public DbSet<FeedRunTimesRow> FeedRunTimes => Set<FeedRunTimesRow>();
	public DbSet<DeletedFeedRow> DeletedFeeds => Set<DeletedFeedRow>();
	public DbSet<BlocklistRow> Blocklist => Set<BlocklistRow>();
	public DbSet<WantlistRow> Wantlist => Set<WantlistRow>();
	public DbSet<WantlistOutboxRow> WantlistOutbox => Set<WantlistOutboxRow>();
	public DbSet<JobRow> Jobs => Set<JobRow>();
	public DbSet<NotificationTargetRow> NotificationTargets => Set<NotificationTargetRow>();
	public DbSet<NotificationSubscriptionRow> NotificationSubscriptions => Set<NotificationSubscriptionRow>();
	public DbSet<NotificationDeliveryOutcomeRow> NotificationDeliveryOutcomes => Set<NotificationDeliveryOutcomeRow>();
	public DbSet<RatingDefaultRow> RatingDefaults => Set<RatingDefaultRow>();
	public DbSet<ImdbRatingRow> ImdbRatings => Set<ImdbRatingRow>();
	public DbSet<DatasetLoadRow> DatasetLoads => Set<DatasetLoadRow>();
	public DbSet<DatasetCheckRow> DatasetChecks => Set<DatasetCheckRow>();
	public DbSet<AnimeIdRow> AnimeIds => Set<AnimeIdRow>();
	public DbSet<RemoteUsageRow> RemoteUsage => Set<RemoteUsageRow>();
	public DbSet<RemoteAttemptRow> RemoteAttempts => Set<RemoteAttemptRow>();
	public DbSet<HousekeepingReportRow> HousekeepingReports => Set<HousekeepingReportRow>();
	public DbSet<IdentityConflictRow> IdentityConflicts => Set<IdentityConflictRow>();

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder
			.UseSeeding((context, _) => EnsureSeeds(context))
			.UseAsyncSeeding((context, _, cancellationToken) => EnsureSeedsAsync(context, cancellationToken));
	}

	private static void EnsureSeeds(DbContext context)
	{
		var settings = context.Set<ProviderConfigRow>();
		if (!settings.Any())
		{
			settings.Add(ProviderConfigRow.Default);
		}

		var roles = context.Set<IdentityRole<Guid>>();
		var existing = roles.Select(role => role.Name).ToList();
		roles.AddRange(MissingRoles(existing));
		context.SaveChanges();
	}

	private static async Task EnsureSeedsAsync(DbContext context, CancellationToken cancellationToken)
	{
		var settings = context.Set<ProviderConfigRow>();
		if (!await settings.AnyAsync(cancellationToken))
		{
			settings.Add(ProviderConfigRow.Default);
		}

		var roles = context.Set<IdentityRole<Guid>>();
		var existing = await roles.Select(role => role.Name).ToListAsync(cancellationToken);
		roles.AddRange(MissingRoles(existing));
		await context.SaveChangesAsync(cancellationToken);
	}

	private static IEnumerable<IdentityRole<Guid>> MissingRoles(IReadOnlyCollection<string?> existing)
	{
		return Enum.GetNames<UserRole>()
			.Where(name => !existing.Contains(name))
			.Select(name => new IdentityRole<Guid>(name) { Id = Guid.NewGuid(), NormalizedName = name.ToUpperInvariant() });
	}

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		propertyConventions.ToList().ForEach(convention => configurationBuilder.Conventions.Add(_ => convention));

		if (Database.IsSqlite())
		{
			configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
			configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
		}
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelConfigurations.ToList().ForEach(builder => builder.Apply(modelBuilder));

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
		var now = dateTimeProvider.Now;
		ChangeTracker.Entries<IAuditable>().ToList().ForEach(entry => entityAuditer.UpdateTimestamps(entry, now));
		ChangeTracker.Entries<IUserOwned>().ToList().ForEach(entry => userOwnershipStamper.Stamp(entry, CurrentUserId));
	}
}
