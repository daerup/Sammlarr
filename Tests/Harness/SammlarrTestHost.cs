using Contracts;
using Contracts.Identity;
using DB;
using DB.Data.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using TestInfrastructure;

namespace Harness;

public sealed class SammlarrTestHost : IAsyncDisposable
{
	private static readonly SqliteConnection Template = BuildTemplate();
	private static readonly Lock BackupGate = new();

	private readonly ServiceProvider _serviceProvider = BuildProvider();
	private readonly SqliteConnection _connection = RestoreFromTemplate();
	private SammlarrDbContext? _context;
	private IDbContextFactory<SammlarrDbContext>? _contextFactory;
	private RawDbContext? _rawContext;

	internal TestCurrentUser CurrentUser { get; } = new();

	internal SammlarrDbContext Context => _context ??= BuildContext(_serviceProvider, CurrentUser, _connection);

	internal IDbContextFactory<SammlarrDbContext> ContextFactory => _contextFactory ??= new SharedConnectionFactory(this);

	internal RawDbContext RawContext => _rawContext ??= new RawDbContext(Context);

	public async ValueTask DisposeAsync()
	{
		if (_context is not null)
		{
			await _context.DisposeAsync();
		}

		await _serviceProvider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	private static SqliteConnection RestoreFromTemplate()
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();
		lock (BackupGate)
		{
			Template.BackupDatabase(connection);
		}

		return connection;
	}

	private static SammlarrDbContext BuildContext(IServiceProvider provider, ICurrentUser user, SqliteConnection connection) =>
		new(
			new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite(connection).Options,
			new EntityAuditer(),
			new UserOwnershipStamper(),
			user,
			provider.GetRequiredService<IEnumerable<IPropertyAddedConvention>>(),
			provider.GetRequiredService<IEnumerable<IModelConfiguration>>(),
			new FixedClock()
		);

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddModule<DbModule>();
		services.AddDataProtection().UseEphemeralDataProtectionProvider();
		return services.BuildServiceProvider();
	}

	private static SqliteConnection BuildTemplate()
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();
		using var provider = BuildProvider();
		var user = new TestCurrentUser();
		using var context = BuildContext(provider, user, connection);
		context.Database.Migrate();
		SeedUser(context, user);
		return connection;
	}

	private static void SeedUser(SammlarrDbContext context, TestCurrentUser user)
	{
		context.Users.Add(
			new UserRow
			{
				Id = user.Id.Value,
				Provider = AuthProvider.Plex,
				ExternalUserId = user.Id.Value.ToString(),
				DisplayName = user.DisplayName,
				AvatarUrl = user.AvatarUrl,
				Role = user.Role,
				JoinedAt = DateTimeOffset.UnixEpoch,
			}
		);
		context.SaveChanges();
	}

	private sealed class FixedClock : IDateTimeProvider
	{
		public DateTimeOffset Now { get; } = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
	}

	private sealed class SharedConnectionFactory(SammlarrTestHost host) : IDbContextFactory<SammlarrDbContext>
	{
		public SammlarrDbContext CreateDbContext() => BuildContext(host._serviceProvider, host.CurrentUser, host._connection);
	}
}
