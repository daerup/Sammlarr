using DB;
using DB.Data.Identity;
using Domain.Providers;
using Integration.Tests.Infrastructure.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;

namespace Integration.Tests.Infrastructure.Hosting;

public sealed class SammlarrScenarioContext : IDisposable
{
	private readonly SqliteConnection _connection;
	private readonly ServiceProvider _rootProvider;
	private readonly IServiceScope _scope;

	internal HttpStub HttpHandler { get; } = new();

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
		services.AddModule<IntegrationTestModule>(HttpHandler, _connection);
		_rootProvider = services.BuildServiceProvider();
		_scope = _rootProvider.CreateScope();

		DbContext.Database.Migrate();
		SeedUser(Resolve<TestCurrentUser>());
	}

	internal void SeedUser(TestCurrentUser user)
	{
		if (DbContext.Users.Any(row => row.Id == user.Id.Value))
		{
			return;
		}

		DbContext.Users.Add(
			new UserRow
			{
				Id = user.Id.Value,
				Provider = AuthProvider.Plex,
				ExternalUserId = user.Id.Value.ToString(),
				DisplayName = user.DisplayName,
				AvatarUrl = user.AvatarUrl,
				Role = user.Role,
			}
		);
		DbContext.SaveChanges();
	}

	public void Dispose()
	{
		_scope.Dispose();
		_rootProvider.Dispose();
		_connection.Dispose();
	}
}
