using NetArchTest.Rules;

namespace Architecture.Tests;

public class DependencyTests
{
	[Test]
	public async Task Shared_is_the_base_kernel()
	{
		await Projects.Shared.DependsOnNoProjects().AndPackages(Packages.DependencyInjection, Packages.Options, Packages.Configuration);
	}

	[Test]
	public async Task Domain_only_depends_on_the_kernel()
	{
		await Projects.Domain.OnlyDependsOn(Projects.Shared).AndPackages(Packages.Cronos, Packages.Vogen);
	}

	[Test]
	public async Task Contracts_has_no_infrastructure_dependencies()
	{
		await Projects.Contracts.OnlyDependsOn(Projects.Shared, Projects.Domain);
	}

	[Test]
	public async Task Observability_depends_only_on_the_kernel_and_contracts()
	{
		await Projects
			.Observability.OnlyDependsOn(Projects.Shared, Projects.Contracts, Projects.Domain)
			.AndPackages(Packages.OpenTelemetry, Packages.Serilog, Packages.DependencyInjection, Packages.Hosting, Packages.Logging);
	}

	[Test]
	public async Task Core_does_not_depend_on_sibling_infrastructure_or_UI()
	{
		await Projects.Core.MustNotDependOn(Projects.Db, Projects.Api, Projects.Ui);
	}

	[Test]
	public async Task DB_does_not_depend_on_sibling_infrastructure_or_UI()
	{
		await Projects.Db.MustNotDependOn(Projects.Core, Projects.Api, Projects.Ui);
	}

	[Test]
	public async Task API_does_not_depend_on_sibling_infrastructure_or_UI()
	{
		await Projects.Api.MustNotDependOn(Projects.Core, Projects.Db, Projects.Ui);
	}

	[Test]
	public async Task Rules_run_against_non_empty_assemblies()
	{
		await Projects.All.Where(assembly => !Types.InAssembly(assembly).GetTypes().Any()).Select(assembly => assembly.GetName().Name).AssertNone();
	}
}
