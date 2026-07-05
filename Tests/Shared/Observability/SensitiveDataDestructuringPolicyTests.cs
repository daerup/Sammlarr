using Domain;
using Observability.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Shared.Tests.Observability;

public class SensitiveDataDestructuringPolicyTests
{
	private sealed record Account(string Username, [property: Sensitive] string Token);

	private sealed record Plain(string Name);

	[Test]
	public async Task Destructure_MasksSensitiveProperty_AndKeepsOthers()
	{
		var captured = new CapturingSink();
		using var logger = new LoggerConfiguration().Destructure.With(new SensitiveDataDestructuringPolicy()).WriteTo.Sink(captured).CreateLogger();

		logger.Information("Loaded {@Account}", new Account("john", "super-secret-token"));

		var structure = (StructureValue)captured.Last.Properties["Account"];
		var username = ((ScalarValue)structure.Properties.Single(p => p.Name == "Username").Value).Value;
		var token = structure.Properties.Single(p => p.Name == "Token").Value.ToString();

		await Assert.That(username).IsEqualTo("john");
		await Assert.That(token).DoesNotContain("super-secret-token");
		await Assert.That(captured.Last.RenderMessage()).DoesNotContain("super-secret-token");
	}

	[Test]
	public async Task Destructure_LeavesTypesWithoutSensitiveProperties_Intact()
	{
		var captured = new CapturingSink();
		using var logger = new LoggerConfiguration().Destructure.With(new SensitiveDataDestructuringPolicy()).WriteTo.Sink(captured).CreateLogger();

		logger.Information("Loaded {@Plain}", new Plain("hello"));

		var structure = (StructureValue)captured.Last.Properties["Plain"];
		var name = ((ScalarValue)structure.Properties.Single(p => p.Name == "Name").Value).Value;

		await Assert.That(name).IsEqualTo("hello");
	}

	private sealed class CapturingSink : ILogEventSink
	{
		private readonly List<LogEvent> _events = [];

		public LogEvent Last => _events[^1];

		public void Emit(LogEvent logEvent) => _events.Add(logEvent);
	}
}
