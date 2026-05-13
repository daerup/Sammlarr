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
	[Arguments(TelemetryVerbosity.Standard, LogLevel.Information)]
	[Arguments(TelemetryVerbosity.Quiet, LogLevel.Warning)]
	public async Task SammlarrCategory_FollowsVerbosity(TelemetryVerbosity verbosity, LogLevel expected)
	{
		var options = BuildFilterOptions(verbosity);

		var rule = options.Rules.Single(r => r.CategoryName == "Sammlarr");
		await Assert.That(rule.LogLevel).IsEqualTo(expected);
	}

	[Test]
	public async Task MinLevel_IsWarning_AndExtensionsHttpAlwaysWarning()
	{
		var options = BuildFilterOptions(TelemetryVerbosity.Verbose);

		await Assert.That(options.MinLevel).IsEqualTo(LogLevel.Warning);
		await Assert.That(options.Rules.Single(r => r.CategoryName == "Microsoft.Extensions.Http").LogLevel).IsEqualTo(LogLevel.Warning);
	}

	[Test]
	[Arguments(TelemetryVerbosity.Verbose, LogLevel.Debug)]
	[Arguments(TelemetryVerbosity.Standard, LogLevel.Warning)]
	[Arguments(TelemetryVerbosity.Quiet, LogLevel.Warning)]
	public async Task FrameworkCategories_FollowVerbosityGate(TelemetryVerbosity verbosity, LogLevel expected)
	{
		var options = BuildFilterOptions(verbosity);
		var ef = options.Rules.Single(r => r.CategoryName == "Microsoft.EntityFrameworkCore");
		var aspnet = options.Rules.Single(r => r.CategoryName == "Microsoft.AspNetCore");

		await Assert.That(ef.LogLevel).IsEqualTo(expected);
		await Assert.That(aspnet.LogLevel).IsEqualTo(expected);
	}

	[Test]
	public async Task SammlarrActivity_HasNoOwnRule_AndInheritsFromSammlarr()
	{
		var options = BuildFilterOptions(TelemetryVerbosity.Quiet);
		await Assert.That(options.Rules.Any(r => r.CategoryName == "Sammlarr.Activity")).IsFalse();
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
