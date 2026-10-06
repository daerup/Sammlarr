using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DB;

internal sealed class DbInitializer(IServiceProvider serviceProvider, ILogger<DbInitializer> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		await serviceProvider.MigrateDatabaseAsync(cancellationToken);
		logger.LogInformation("Database migrations applied");
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
