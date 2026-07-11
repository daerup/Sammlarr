using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DB;

internal sealed class DbInitializer(IServiceProvider serviceProvider, ILogger<DbInitializer> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		await using var scope = serviceProvider.CreateAsyncScope();
		var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SammlarrDbContext>>();
		await using var ctx = await factory.CreateDbContextAsync(cancellationToken);
		await ctx.Database.MigrateAsync(cancellationToken);
		logger.LogInformation("Database migrations applied");
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
