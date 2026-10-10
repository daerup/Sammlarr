using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DB;

internal sealed class DbInitializer(IServiceProvider serviceProvider, ILogger<DbInitializer> logger) : IHostedService
{
	private const int ConstraintFailed = 19;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			await serviceProvider.MigrateDatabaseAsync(cancellationToken);
		}
		catch (SqliteException exception) when (exception.SqliteErrorCode == ConstraintFailed)
		{
			logger.LogCritical(exception, "This database holds identities this version can't keep; delete it or use the staging Reset");
			throw;
		}

		logger.LogInformation("Database migrations applied");
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
