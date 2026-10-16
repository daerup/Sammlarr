using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace Core.Housekeeping;

internal sealed class HousekeepingSchedule(IServiceProvider services, ILogger<HousekeepingSchedule> logger) : IHostedLifecycleService
{
	internal const string Nightly = "0 0 3 * * *";

	internal static readonly Guid Id = new(SHA256.HashData(Encoding.UTF8.GetBytes("housekeeping"))[..16]);

	public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public async Task StartedAsync(CancellationToken cancellationToken)
	{
		await using var scope = services.CreateAsyncScope();
		var nightly = scope.ServiceProvider.GetRequiredService<ICronTickerManager<CronTickerEntity>>();
		await nightly.DeleteAsync(Id, cancellationToken);
		var added = await nightly.AddAsync(
			new CronTickerEntity
			{
				Id = Id,
				Function = HousekeepingTicker.FunctionName,
				Expression = Nightly,
			},
			cancellationToken
		);
		if (!added.IsSucceeded)
		{
			logger.LogWarning(added.Exception, "The nightly housekeeping pass could not be scheduled");
		}
	}

	public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
