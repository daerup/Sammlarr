using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Options;
using Shared.Telemetry;

namespace Shared.Tests.Unit.Telemetry;

public class TelemetryFilterRulesTests
{
	[Test]
	[Arguments(TelemetryVerbosity.Verbose, LogLevel.Debug)]
	[Arguments(TelemetryVerbosity.Normal, LogLevel.Information)]
	[Arguments(TelemetryVerbosity.Quiet, LogLevel.Warning)]
	public async Task SammlarrCategory_FollowsVerbosity(TelemetryVerbosity verbosity, LogLevel expected)
	{
		var options = BuildFilterOptions(verbosity);

		var rule = options.Rules.Single(r => r.CategoryName == "Sammlarr");
		await Assert.That(rule.LogLevel).IsEqualTo(expected);
	}

	[Test]
	public async Task FrameworkCategories_AreCappedRegardlessOfVerbosity()
	{
		var options = BuildFilterOptions(TelemetryVerbosity.Verbose);

		await Assert.That(options.MinLevel).IsEqualTo(LogLevel.Warning);
		await Assert.That(options.Rules.Single(r => r.CategoryName == "Microsoft.EntityFrameworkCore").LogLevel).IsEqualTo(LogLevel.Information);
		await Assert.That(options.Rules.Single(r => r.CategoryName == "Microsoft.AspNetCore").LogLevel).IsEqualTo(LogLevel.Information);
		await Assert.That(options.Rules.Single(r => r.CategoryName == "Microsoft.Extensions.Http").LogLevel).IsEqualTo(LogLevel.Warning);
	}

	private static LoggerFilterOptions BuildFilterOptions(TelemetryVerbosity verbosity)
	{
		var services = new ServiceCollection();
		services.AddSingleton(new TelemetryOptions { Verbosity = verbosity });
		new TelemetryModule().RegisterServices(services);

		using var sp = services.BuildServiceProvider();
		return sp.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;
	}
}
