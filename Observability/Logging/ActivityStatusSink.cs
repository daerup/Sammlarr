using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Observability.Logging;

internal sealed class ActivityStatusSink : ILogEventSink
{
	public void Emit(LogEvent logEvent)
	{
		if (logEvent.Exception is null || Activity.Current is not { } activity)
		{
			return;
		}
		activity.AddException(logEvent.Exception);
		activity.SetStatus(ActivityStatusCode.Error, logEvent.Exception.Message);
	}
}
