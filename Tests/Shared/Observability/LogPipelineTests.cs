using Microsoft.Extensions.DependencyInjection;
using Observability;
using Observability.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shared.Options;

namespace Shared.Tests.Observability;

public class LogPipelineTests
{
	[Test]
	[Arguments("Web.Staging.StagingRun", true)]
	[Arguments("Core.Media.DetailsFetcher", true)]
	[Arguments("Microsoft.AspNetCore.Hosting.Diagnostics", false)]
	public async Task Configure_ForACategory_KeepsInformationOnlyFromTheApp(string category, bool kept)
	{
		var captured = new CapturingSink();
		var provider = new ServiceCollection()
			.AddSingleton(new LoggingLevelSwitch(LogEventLevel.Information))
			.AddSingleton(new ObservabilityOptions())
			.AddSingleton<ILogEventSink>(captured)
			.BuildServiceProvider();
		var configuration = new LoggerConfiguration();
		LogPipeline.Configure(configuration, provider, new ObservabilityContext(false, "Tests", "0.0.0"));
		using var logger = configuration.CreateLogger();

		logger.ForContext(Constants.SourceContextPropertyName, category).Information("Seeded the staging database");

		await Assert.That(captured.Events.Count > 0).IsEqualTo(kept);
	}

	private sealed class CapturingSink : ILogEventSink
	{
		public List<LogEvent> Events { get; } = [];

		public void Emit(LogEvent logEvent) => Events.Add(logEvent);
	}
}
