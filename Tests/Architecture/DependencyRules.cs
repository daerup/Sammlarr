using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using Assembly = System.Reflection.Assembly;
using TestResult = NetArchTest.Rules.TestResult;

namespace Architecture.Tests;

internal static class DependencyRules
{
	internal static string Namespace(this Assembly assembly) => assembly.GetName().Name!;

	internal static DependencyRule OnlyDependsOn(this Assembly subject, params Assembly[] projects) => new(subject, projects);

	internal static DependencyRule DependsOnNoProjects(this Assembly subject) => subject.OnlyDependsOn();

	internal static Task MustNotDependOn(this Assembly subject, params Assembly[] forbidden) =>
		Types.InAssembly(subject).Should().NotHaveDependencyOnAny([.. forbidden.Select(Namespace)]).GetResult().AssertNone();

	internal static Task AssertNone(this TestResult result) => result.FailingTypes.AssertNone();

	internal static Task AssertNone(this IEnumerable<IType> types) => types.Select(type => type.FullName).AssertNone();

	internal static async Task AssertNone(this IEnumerable<string?> names) => await Assert.That(names).IsEmpty();
}

internal sealed class DependencyRule(Assembly subject, IEnumerable<Assembly> projects)
{
	private readonly List<string> allowed = [Packages.System, subject.Namespace(), .. projects.Select(project => project.Namespace())];

	internal DependencyRule AndPackages(params string[] packages)
	{
		allowed.AddRange(packages);
		return this;
	}

	internal TaskAwaiter GetAwaiter() => Check().GetAwaiter();

	private Task Check() => Types.InAssembly(subject).Should().OnlyHaveDependencyOn([.. allowed]).GetResult().AssertNone();
}
