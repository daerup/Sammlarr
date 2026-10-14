using Serilog.Core;
using Serilog.Events;

namespace TestInfrastructure;

public sealed class CapturingSink : ILogEventSink
{
	private readonly List<LogEvent> _events = [];

	public IReadOnlyList<LogEvent> Events => _events;

	public LogEvent Last => _events[^1];

	public void Emit(LogEvent logEvent) => _events.Add(logEvent);
}
