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
		var created = ctx.Initialize();
		logger.LogInformation("Database schema ensured (freshly created={Created})", created);
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
