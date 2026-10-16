using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DB;

public static class DatabaseMigrationExtensions
{
	extension(IServiceProvider services)
	{
		public async Task MigrateDatabaseAsync(CancellationToken cancellationToken = default)
		{
			await using var scope = services.CreateAsyncScope();
			var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SammlarrDbContext>>();
			await using var context = await factory.CreateDbContextAsync(cancellationToken);
			await context.Database.MigrateAsync(cancellationToken);
		}
	}
}
