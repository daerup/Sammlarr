using Contracts;
using DB.Data.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using TestInfrastructure;

namespace DB.Tests.Integration;

public class SQLiteFixture : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly ServiceProvider _serviceProvider;

	public SQLiteFixture()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddModule<DbModule>();
		services.AddDataProtection().UseEphemeralDataProtectionProvider();
		_serviceProvider = services.BuildServiceProvider();
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
	}

	internal TestCurrentUser CurrentUser { get; } = new();
	internal SammlarrDbContext Context => field ??= CreateDbContext();
	internal RawDbContext RawContext => field ??= new RawDbContext(Context);
	internal IDbContextFactory<SammlarrDbContext> ContextFactory => field ??= new SharedConnectionFactory(this);

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
		await _serviceProvider.DisposeAsync();
	}

	private DbContextOptions<SammlarrDbContext> BuildOptions() => new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite(_connection).Options;

	private SammlarrDbContext CreateDbContext()
	{
		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Getter.Returns(DateTimeOffset.UtcNow);
		var ctx = new SammlarrDbContext(
			BuildOptions(),
			new EntityAuditer(),
			new UserOwnershipStamper(),
			CurrentUser,
			_serviceProvider.GetRequiredService<IEnumerable<IPropertyAddedConvention>>(),
			_serviceProvider.GetRequiredService<IEnumerable<IModelConfiguration>>(),
			dateTimeProvider
		);
		ctx.Database.Migrate();
		SeedCurrentUser(ctx);
		return ctx;
	}

	private void SeedCurrentUser(SammlarrDbContext ctx)
	{
		if (ctx.Users.Any(user => user.Id == CurrentUser.Id.Value))
		{
			return;
		}

		ctx.Users.Add(
			new UserRow
			{
				Id = CurrentUser.Id.Value,
				Provider = AuthProvider.Plex,
				ExternalUserId = CurrentUser.Id.Value.ToString(),
				DisplayName = CurrentUser.DisplayName,
				AvatarUrl = CurrentUser.AvatarUrl,
				Role = CurrentUser.Role,
				JoinedAt = DateTimeOffset.UnixEpoch,
			}
		);
		ctx.SaveChanges();
	}

	private sealed class SharedConnectionFactory(SQLiteFixture fixture) : IDbContextFactory<SammlarrDbContext>
	{
		public SammlarrDbContext CreateDbContext() => fixture.CreateDbContext();
	}
}
