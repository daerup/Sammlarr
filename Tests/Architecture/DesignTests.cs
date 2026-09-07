using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NetArchTest.Rules;

namespace Architecture.Tests;

public class DesignTests
{
	[Test]
	public async Task Domain_types_are_immutable()
	{
		var result = Types.InAssembly(Projects.Domain).That().AreClasses().Should().BeImmutable().GetResult();

		await result.FailingTypes.Select(type => $"{type.FullName} ({type.Explanation})").AssertNone();
	}

	[Test]
	public async Task Pure_infrastructure_exposes_only_service_modules_publicly()
	{
		await Types
			.InAssemblies([Projects.Api, Projects.Db])
			.That()
			.ArePublic()
			.And()
			.AreNotNested()
			.And()
			.DoNotImplementInterface<Shared.DI.IServiceModule>()
			.And()
			.DoNotInherit<Migration>()
			.And()
			.DoNotInherit<ModelSnapshot>()
			.GetTypes()
			.AssertNone();
	}

	[Test]
	public async Task Repositories_and_services_are_internal_and_sealed()
	{
		await new[] { "Repository", "Service" }
			.SelectMany(suffix =>
				Types
					.InAssemblies([Projects.Core, Projects.Db, Projects.Api])
					.That()
					.AreClasses()
					.And()
					.HaveNameEndingWith(suffix)
					.Should()
					.NotBePublic()
					.And()
					.BeSealed()
					.GetResult()
					.FailingTypes
			)
			.AssertNone();
	}

	[Test]
	public async Task Repository_and_service_ports_live_in_Contracts()
	{
		var outsideContracts = Projects.All.Where(assembly => assembly != Projects.Contracts).ToArray();

		await new[] { "Repository", "Service" }
			.SelectMany(suffix => Types.InAssemblies(outsideContracts).That().AreInterfaces().And().ArePublic().And().HaveNameEndingWith(suffix).GetTypes())
			.AssertNone();
	}

	[Test]
	public async Task Public_methods_have_no_boolean_parameters()
	{
		await Methods
			.Public.Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(bool)))
			.Select(Methods.Describe)
			.AssertNone();
	}

	[Test]
	public async Task Public_methods_have_at_most_four_parameters()
	{
		await Methods
			.Public.Where(method => method.GetParameters().Count(parameter => parameter.ParameterType != typeof(CancellationToken)) > 4)
			.Select(Methods.Describe)
			.AssertNone();
	}

	[Test]
	public async Task Production_does_not_depend_on_test_infrastructure()
	{
		await Types.InAssemblies(Projects.All).Should().NotHaveDependencyOnAny("TestInfrastructure", Packages.TUnit, Packages.Bogus).GetResult().AssertNone();
	}

	[Test]
	public async Task Source_generators_are_not_referenced_at_runtime()
	{
		await Types.InAssemblies(Projects.All).Should().NotHaveDependencyOnAny(Packages.Roslyn).GetResult().AssertNone();
	}
}
