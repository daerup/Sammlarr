using Observability;

namespace Shared.Tests.Observability;

public class ExporterSelectionTests
{
	[Test]
	[Arguments("http://localhost:4317")]
	public async Task WithEndpoint_UsesOtlp(string endpoint)
	{
		await Assert.That(ObservabilityContext.ShouldUseOtlp(endpoint)).IsTrue();
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task WithoutEndpoint_UsesConsole(string? endpoint)
	{
		await Assert.That(ObservabilityContext.ShouldUseOtlp(endpoint)).IsFalse();
	}
}
