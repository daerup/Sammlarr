using DB.Encryption;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Tests.Integration;

internal class SQLiteFixture : IAsyncDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly SqliteConnection _connection;

	internal SammlarrDbContext Context => field ??= CreateDbContext();
	internal RawDbContext RawContext => field ??= new RawDbContext(Context);

	public SQLiteFixture()
	{
		var services = new ServiceCollection();
		services.AddDataProtection();
		services.AddScoped<IDataProtectionService, DataProtectionService>();
		_serviceProvider = services.BuildServiceProvider();
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
	}

	private DbContextOptions<SammlarrDbContext> BuildOptions() => new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite(_connection).Options;

	public SammlarrDbContext CreateDbContext()
	{
		var ctx = new SammlarrDbContext(BuildOptions(), _serviceProvider.GetRequiredService<IDataProtectionService>());
		ctx.Database.EnsureCreated();
		return ctx;
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
		await _serviceProvider.DisposeAsync();
	}
}
