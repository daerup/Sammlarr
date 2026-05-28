using API;
using API.Mdblist;
using API.Plex;
using API.Tmdb;
using Contracts;
using Contracts.Media;
using Core;
using DB;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace Integration.Tests.Infrastructure;

public sealed class SammlarrScenarioContext : IDisposable
{
	private readonly SqliteConnection _connection;
	private readonly ServiceProvider _rootProvider;
	private readonly IServiceScope _scope;

	internal TestHttpMessageHandler HttpHandler { get; } = new();

	internal SammlarrDbContext DbContext => _scope.ServiceProvider.GetRequiredService<SammlarrDbContext>();

	public T Resolve<T>()
		where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

	public T ResolveKeyed<T>(object key)
		where T : notnull => _scope.ServiceProvider.GetRequiredKeyedService<T>(key);

	public SammlarrScenarioContext()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();

		var services = new ServiceCollection();
		ConfigureServices(services);
		_rootProvider = services.BuildServiceProvider();
		_scope = _rootProvider.CreateScope();

		DbContext.Initialize();
	}

	public void Dispose()
	{
		_scope.Dispose();
		_rootProvider.Dispose();
		_connection.Dispose();
	}

	private void ConfigureServices(IServiceCollection services)
	{
		// Pre-register before DbModule so EF Core's TryAdd skips its IDbContextFactory registration
		services.AddScoped<IDbContextFactory<SammlarrDbContext>>(sp => new TestDbContextFactory(sp, _connection));
		services.AddScoped<SammlarrDbContext>(sp => sp.GetRequiredService<IDbContextFactory<SammlarrDbContext>>().CreateDbContext());

		services.AddModule<CoreModule>();
		services.AddModule<DbModule>();
		services.AddModule<ApiModule>();

		// Override primary HTTP handler for all typed API clients
		services.AddHttpClient<TmdbClient>().ConfigurePrimaryHttpMessageHandler(_ => HttpHandler);
		services.AddHttpClient<MdblistClient>().ConfigurePrimaryHttpMessageHandler(_ => HttpHandler);
		services.AddHttpClient<PlexClient>().ConfigurePrimaryHttpMessageHandler(_ => HttpHandler);

		services.AddSingleton(new TmdbOptions { BaseUrl = "https://api.test.com", ImageBaseUrl = "https://image.tmdb.org/t/p/original" });
		services.AddSingleton(new MdblistOptions { BaseUrl = "https://mdblist.test.com" });
		services.AddSingleton(
			new PlexOptions
			{
				BaseUrl = "https://plex.test.com",
				Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
			}
		);
	}

	private sealed class TestDbContextFactory(IServiceProvider sp, SqliteConnection connection) : IDbContextFactory<SammlarrDbContext>
	{
		public SammlarrDbContext CreateDbContext() =>
			new(
				new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite(connection).Options,
				sp.GetRequiredService<EntityAuditer>(),
				sp.GetRequiredService<IEnumerable<IPropertyAddedConvention>>(),
				sp.GetRequiredService<IEnumerable<IModelConfiguration>>(),
				sp.GetRequiredService<IDateTimeProvider>()
			);
	}
}
