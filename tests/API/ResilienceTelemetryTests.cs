using Polly.Telemetry;

namespace API.Tests;

public class ResilienceTelemetryTests
{
	[Test]
	public async Task Severity_ShowsHandledAttempts_ButSuppressesHealthyOnes()
	{
		await Assert.That(ResilienceModule.Severity("ExecutionAttempt", ResilienceEventSeverity.Error)).IsEqualTo(ResilienceEventSeverity.Warning);
		await Assert.That(ResilienceModule.Severity("ExecutionAttempt", ResilienceEventSeverity.Debug)).IsEqualTo(ResilienceEventSeverity.None);
	}

	[Test]
	public async Task Severity_RaisesRecoveryEvents_ToWarning()
	{
		foreach (var eventName in new[] { "OnRetry", "OnTimeout", "OnHedging" })
		{
			await Assert.That(ResilienceModule.Severity(eventName, ResilienceEventSeverity.Error)).IsEqualTo(ResilienceEventSeverity.Warning);
		}
	}

	[Test]
	public async Task Severity_PreservesDefault_ForOtherEvents()
	{
		await Assert.That(ResilienceModule.Severity("OnCircuitOpened", ResilienceEventSeverity.Error)).IsEqualTo(ResilienceEventSeverity.Error);
		await Assert.That(ResilienceModule.Severity("PipelineExecuted", ResilienceEventSeverity.Debug)).IsEqualTo(ResilienceEventSeverity.Debug);
	}
}
