using System.Security.Cryptography;
using System.Text;
using Contracts;
using Contracts.Datasets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace Core.Datasets;

internal sealed class DatasetSchedule(IServiceProvider services, IDateTimeProvider clock, ILogger<DatasetSchedule> logger) : IHostedLifecycleService
{
	internal const string Daily = "0 30 4 * * *";

	public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public async Task StartedAsync(CancellationToken cancellationToken)
	{
		await using var scope = services.CreateAsyncScope();
		var daily = scope.ServiceProvider.GetRequiredService<ICronTickerManager<CronTickerEntity>>();
		var once = scope.ServiceProvider.GetRequiredService<ITimeTickerManager<TimeTickerEntity>>();
		var loads = scope.ServiceProvider.GetRequiredService<IDatasetLoads>();
		foreach (var name in scope.ServiceProvider.GetServices<IDatasetLoader>().Select(loader => loader.Name))
		{
			await daily.DeleteAsync(IdOf(name), cancellationToken);
			var added = await daily.AddAsync(Cron(name), cancellationToken);
			Warn(name, "daily", added.IsSucceeded, added.Exception);
			await loads.FindAsync(name, cancellationToken).ApplyNone(() => FirstLoadAsync(once, name, cancellationToken));
		}
	}

	public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private static Guid IdOf(string name) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"dataset:{name}"))[..16]);

	private async Task FirstLoadAsync(ITimeTickerManager<TimeTickerEntity> once, string name, CancellationToken cancellationToken)
	{
		var added = await once.AddAsync(DatasetTicker.Once(name, clock.Now), cancellationToken);
		Warn(name, "first", added.IsSucceeded, added.Exception);
	}

	private void Warn(string name, string load, bool scheduled, Exception? failure)
	{
		if (!scheduled)
		{
			logger.LogWarning(failure, "The {Load} load of {Dataset} could not be scheduled", load, name);
		}
	}

	private static CronTickerEntity Cron(string name)
	{
		return new CronTickerEntity
		{
			Id = IdOf(name),
			Function = DatasetTicker.FunctionName,
			Expression = Daily,
			Request = TickerHelper.CreateTickerRequest(new DatasetRequest(name)),
			IsEnabled = true,
		};
	}
}
