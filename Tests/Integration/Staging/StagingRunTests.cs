using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Staging;

namespace Integration.Tests.Staging;

public class StagingRunTests
{
	[Test]
	public async Task RunAsync_WithARelativeDatabase_RefusesBeforeDeletingOrSeedingAnything()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Sammlarr:ConnectionString"] = "Data Source=sammlarr.db" })
			.Build();
		var run = new StagingRun(new ServiceCollection().BuildServiceProvider(), configuration, NullLogger<StagingRun>.Instance);

		await Assert
			.That(async () => await run.RunAsync(CancellationToken.None))
			.Throws<InvalidOperationException>()
			.WithMessageContaining("absolute path only, not 'sammlarr.db'");
	}
}
