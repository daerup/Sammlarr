using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Shared.Telemetry;

internal sealed class ActivityLogProcessor : BaseProcessor<Activity>
{
	private readonly ILogger _logger;

	public ActivityLogProcessor(ILoggerFactory loggerFactory) => _logger = loggerFactory.CreateLogger("Sammlarr.Activity");

	public override void OnEnd(Activity data)
	{
		if (data.Source.Name != SammlarrTelemetry.ServiceName)
		{
			return;
		}
		if (data.OperationName.StartsWith("blazor.", StringComparison.Ordinal))
		{
			return;
		}
		_logger.LogInformation("{Display} [{Duration:N0}ms]", data.DisplayName, data.Duration.TotalMilliseconds);
	}
}
