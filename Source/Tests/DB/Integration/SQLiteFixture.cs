using Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Tests.Integration;

public class SQLiteFixture : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly ServiceProvider _serviceProvider;

	public SQLiteFixture()
	{
		var services = new ServiceCollection();
		services.AddModule<DbModule>();
		_serviceProvider = services.BuildServiceProvider();
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
	}

	internal SammlarrDbContext Context => field ??= CreateDbContext();
	internal RawDbContext RawContext => field ??= new RawDbContext(Context);

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
		return new SammlarrDbContext(
			BuildOptions(),
			new EntityAuditer(),
			_serviceProvider.GetRequiredService<IEnumerable<IPropertyAddedConvention>>(),
			dateTimeProvider
		);
	}
}
