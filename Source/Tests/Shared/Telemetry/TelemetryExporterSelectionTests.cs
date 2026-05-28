using Shared.Options;
using Shared.Telemetry;

namespace Shared.Tests.Telemetry;

public class TelemetryExporterSelectionTests
{
	[Test]
	public async Task Auto_WithEndpoint_UsesOtlp()
	{
		var options = new TelemetryOptions { Exporter = TelemetryExporter.Auto };

		await Assert.That(TelemetryModule.ShouldUseOtlp(options, "http://localhost:4317")).IsTrue();
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task Auto_WithoutEndpoint_UsesConsole(string? endpoint)
	{
		var options = new TelemetryOptions { Exporter = TelemetryExporter.Auto };

		await Assert.That(TelemetryModule.ShouldUseOtlp(options, endpoint)).IsFalse();
	}

	[Test]
	[Arguments("http://localhost:4317")]
	[Arguments(null)]
	public async Task Console_AlwaysUsesConsole(string? endpoint)
	{
		var options = new TelemetryOptions { Exporter = TelemetryExporter.Console };

		await Assert.That(TelemetryModule.ShouldUseOtlp(options, endpoint)).IsFalse();
	}
}
