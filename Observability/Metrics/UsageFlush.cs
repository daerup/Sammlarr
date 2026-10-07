using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace Observability.Metrics;

internal sealed class UsageFlush(MeterProvider meters) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StopAsync(CancellationToken cancellationToken)
	{
		meters.ForceFlush();
		return Task.CompletedTask;
	}
}
