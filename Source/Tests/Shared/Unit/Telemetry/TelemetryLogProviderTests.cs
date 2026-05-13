using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Shared.Options;
using Shared.Telemetry;

namespace Shared.Tests.Unit.Telemetry;

public class TelemetryLogProviderTests
{
	[Test]
	public async Task ConsoleExporter_RegistersStandardConsoleLoggerProvider()
	{
		using var sp = BuildProvider(TelemetryExporter.Console);

		var providers = sp.GetServices<ILoggerProvider>();

		await Assert.That(providers.Any(p => p is ConsoleLoggerProvider)).IsTrue();
	}

	[Test]
	public async Task AutoExporter_WithoutEndpoint_RegistersStandardConsoleLoggerProvider()
	{
		Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);

		using var sp = BuildProvider(TelemetryExporter.Auto);

		var providers = sp.GetServices<ILoggerProvider>();

		await Assert.That(providers.Any(p => p is ConsoleLoggerProvider)).IsTrue();
	}

	private static ServiceProvider BuildProvider(TelemetryExporter exporter)
	{
		var services = new ServiceCollection();
		services.AddSingleton(new TelemetryOptions { Exporter = exporter });
		new TelemetryModule().RegisterServices(services);
		return services.BuildServiceProvider();
	}
}
