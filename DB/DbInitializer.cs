using Domain.Feed;
using Domain.Feed.Run;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
		var interrupted = await SweepInterruptedRunsAsync(cancellationToken);
		if (interrupted > 0)
		{
			logger.LogWarning("{Count} feed runs were still going when Sammlarr stopped; they are marked as stopped", interrupted);
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private async Task<int> SweepInterruptedRunsAsync(CancellationToken cancellationToken)
	{
		await using var scope = serviceProvider.CreateAsyncScope();
		var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SammlarrDbContext>>();
		await using var context = await factory.CreateDbContextAsync(cancellationToken);
		return await context
			.FeedRuns.IgnoreQueryFilters()
			.Where(run => run.Conclusiveness == Conclusiveness.Pending)
			.ExecuteUpdateAsync(
				set => set.SetProperty(run => run.Conclusiveness, Conclusiveness.Stopped).SetProperty(run => run.SourceFailure, FeedRun.Interrupted),
				cancellationToken
			);
	}
}
