using System.Reflection;
using Domain.Announcing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NetArchTest.Rules;
using Vogen;

namespace Architecture.Tests;

public class DesignTests
{
	[Test]
	public async Task Design_OfDomainTypes_IsImmutable()
	{
		var result = Types.InAssembly(Projects.Domain).That().AreClasses().Should().BeImmutable().GetResult();

		await result.FailingTypes.Select(type => $"{type.FullName} ({type.Explanation})").AssertNone();
	}

	[Test]
	public async Task Design_OfPureInfrastructure_ExposesOnlyItsCompositionRootsPublicly()
	{
		await Types
			.InAssemblies([Projects.Api, Projects.Db])
			.That()
			.ArePublic()
			.And()
			.AreNotNested()
			.And()
			.DoNotHaveNameEndingWith("Extensions")
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
	public async Task Design_OfRepositoriesAndServices_IsInternalAndSealed()
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
	public async Task Design_OfRepositoryAndServicePorts_PlacesThemInContracts()
	{
		var outsideContracts = Projects.All.Where(assembly => assembly != Projects.Contracts).ToArray();

		await new[] { "Repository", "Service" }
			.SelectMany(suffix => Types.InAssemblies(outsideContracts).That().AreInterfaces().And().ArePublic().And().HaveNameEndingWith(suffix).GetTypes())
			.AssertNone();
	}

	[Test]
	public async Task Design_OfPublicMethods_ForbidsBooleanParameters()
	{
		await Methods
			.Public.Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(bool)))
			.Select(Methods.Describe)
			.AssertNone();
	}

	[Test]
	public async Task Design_OfPublicMethods_AllowsAtMostFourParameters()
	{
		await Methods
			.Public.Where(method => method.GetParameters().Count(parameter => parameter.ParameterType != typeof(CancellationToken)) > 4)
			.Select(Methods.Describe)
			.AssertNone();
	}

	[Test]
	public async Task Design_OfProduction_ExcludesTestInfrastructure()
	{
		await Types.InAssemblies(Projects.All).Should().NotHaveDependencyOnAny("TestInfrastructure", Packages.TUnit, Packages.Bogus).GetResult().AssertNone();
	}

	[Test]
	public async Task Design_OfSourceGenerators_ExcludesThemAtRuntime()
	{
		await Types.InAssemblies(Projects.All).Should().NotHaveDependencyOnAny(Packages.Roslyn).GetResult().AssertNone();
	}

	[Test]
	public async Task Design_OfAnnouncedFacts_IsEitherAnEventOrANotification()
	{
		await Types.InAssemblies(Projects.All).That().ImplementInterface<IEvent>().And().ImplementInterface<INotification>().GetTypes().AssertNone();
	}

	[Test]
	public async Task Design_OfGuidIds_IsAlwaysAVogenValueObject()
	{
		var ids = Projects
			.Domain.GetTypes()
			.Where(type => type.IsValueType && type.Name.EndsWith("Id", StringComparison.Ordinal))
			.Where(type => type.GetProperty("Value")?.PropertyType == typeof(Guid))
			.ToList();

		await Assert.That(ids).Contains(typeof(Domain.Media.MediaId));
		await ids.Where(type => type.GetCustomAttribute<ValueObjectAttribute<Guid>>() is null).Select(type => type.FullName).AssertNone();
	}
}
