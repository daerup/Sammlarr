using Microsoft.Extensions.DependencyInjection;
using Observability;
using Observability.Logging;
using Serilog;
using Serilog.Core;
using Shared.Options;
using TestInfrastructure;

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
		using var logger = Pipeline(captured);

		logger.ForContext(Constants.SourceContextPropertyName, category).Information("Seeded the staging database");

		await Assert.That(captured.Events.Count > 0).IsEqualTo(kept);
	}

	[Test]
	public async Task Configure_ForTheUnencryptedKeyNotice_DropsItButKeepsOtherKeyWarnings()
	{
		var captured = new CapturingSink();
		using var logger = Pipeline(captured);
		var keys = logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager");

		keys.Warning("No XML encryptor configured. Key {KeyId:B} may be persisted to storage in unencrypted form.", Guid.NewGuid());
		keys.Warning("An exception occurred while processing the key element {Element}.", "<key />");

		await Assert
			.That(captured.Events.Select(logEvent => logEvent.MessageTemplate.Text))
			.IsEquivalentTo(["An exception occurred while processing the key element {Element}."]);
	}

	private static Logger Pipeline(CapturingSink captured)
	{
		var provider = new ServiceCollection()
			.AddSingleton(new LoggingLevelSwitch())
			.AddSingleton(new ObservabilityOptions())
			.AddSingleton<ILogEventSink>(captured)
			.BuildServiceProvider();
		var configuration = new LoggerConfiguration();
		LogPipeline.Configure(configuration, provider, new ObservabilityContext(false, "Tests", "0.0.0"));
		return configuration.CreateLogger();
	}
}
