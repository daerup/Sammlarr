using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Observability.Feed;
using OpenTelemetry;

namespace Observability.Tracing;

internal sealed class ActivityLogProcessor : BaseProcessor<Activity>
{
	private readonly ILogger _logger;

	public ActivityLogProcessor(ILoggerFactory loggerFactory) => _logger = loggerFactory.CreateLogger("Sammlarr.Activity");

	public override void OnEnd(Activity data)
	{
		if (data.Source.Name is not (Instrumentation.ServiceName or FeedRunSpans.SourceName))
		{
			return;
		}
		if (data.Status != ActivityStatusCode.Error)
		{
			return;
		}
		_logger.LogWarning("Trace {Display} errored [{Duration:N0}ms]: {Reason}", data.DisplayName, data.Duration.TotalMilliseconds, data.StatusDescription);
	}
}
