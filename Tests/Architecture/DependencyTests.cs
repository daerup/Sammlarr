using NetArchTest.Rules;

namespace Architecture.Tests;

public class DependencyTests
{
	[Test]
	public async Task Dependencies_OfShared_MakeItTheBaseKernel()
	{
		await Projects.Shared.DependsOnNoProjects().AndPackages(Packages.DependencyInjection, Packages.Options, Packages.Configuration);
	}

	[Test]
	public async Task Dependencies_OfDomain_AreOnlyTheKernel()
	{
		await Projects.Domain.OnlyDependsOn(Projects.Shared).AndPackages(Packages.Cronos, Packages.Vogen);
	}

	[Test]
	public async Task Dependencies_OfContracts_ExcludeInfrastructure()
	{
		await Projects.Contracts.OnlyDependsOn(Projects.Shared, Projects.Domain);
	}

	[Test]
	public async Task Dependencies_OfObservability_AreOnlyTheKernelAndContracts()
	{
		await Projects
			.Observability.OnlyDependsOn(Projects.Shared, Projects.Contracts, Projects.Domain)
			.AndPackages(Packages.OpenTelemetry, Packages.Serilog, Packages.DependencyInjection, Packages.Hosting, Packages.Logging);
	}

	[Test]
	public async Task Dependencies_OfCore_ExcludeSiblingInfrastructureAndUi()
	{
		await Projects.Core.MustNotDependOn(Projects.Db, Projects.Api, Projects.Ui);
	}

	[Test]
	public async Task Dependencies_OfDb_ExcludeSiblingInfrastructureAndUi()
	{
		await Projects.Db.MustNotDependOn(Projects.Core, Projects.Api, Projects.Ui);
	}

	[Test]
	public async Task Dependencies_OfApi_ExcludeSiblingInfrastructureAndUi()
	{
		await Projects.Api.MustNotDependOn(Projects.Core, Projects.Db, Projects.Ui);
	}

	[Test]
	public async Task Dependencies_WhenRulesRun_TargetNonEmptyAssemblies()
	{
		await Projects.All.Where(assembly => !Types.InAssembly(assembly).GetTypes().Any()).Select(assembly => assembly.GetName().Name).AssertNone();
	}
}
