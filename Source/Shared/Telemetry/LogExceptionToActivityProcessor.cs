using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Shared.Telemetry;

internal sealed class LogExceptionToActivityProcessor : BaseProcessor<LogRecord>
{
	public override void OnEnd(LogRecord data)
	{
		if (data.Exception is null || Activity.Current is not { } activity)
		{
			return;
		}
		activity.AddException(data.Exception);
		activity.SetStatus(ActivityStatusCode.Error, data.Exception.Message);
	}
}
