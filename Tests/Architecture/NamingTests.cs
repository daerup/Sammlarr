using System.Reflection;
using NetArchTest.Rules;

namespace Architecture.Tests;

public class NamingTests
{
	[Test]
	public async Task Naming_OfInterfaces_RequiresAnIPrefix()
	{
		await Types.InAssemblies(Projects.All).That().AreInterfaces().Should().HaveNameStartingWith("I").GetResult().AssertNone();
	}

	[Test]
	public async Task Naming_OfServiceModules_RequiresAModuleSuffix()
	{
		await Types.InAssemblies(Projects.All).That().ImplementInterface<Shared.DI.IServiceModule>().Should().HaveNameEndingWith("Module").GetResult().AssertNone();
	}

	[Test]
	public async Task Naming_OfExceptions_RequiresAnExceptionSuffix()
	{
		await Types.InAssemblies(Projects.All).That().Inherit<Exception>().Should().HaveNameEndingWith("Exception").GetResult().AssertNone();
	}

	[Test]
	public async Task Naming_OfAsyncMethods_RequiresAnAsyncOrAwaitSuffix()
	{
		await Methods.Public.Where(ViolatesAsyncNaming).Select(Methods.Describe).AssertNone();
	}

	[Test]
	public async Task Naming_OfTheAsyncSuffix_RestrictsItToAsyncMethods()
	{
		await Methods
			.Public.Where(method => method.Name.EndsWith("Async", StringComparison.Ordinal))
			.Where(method => !IsAsync(method.ReturnType))
			.Select(Methods.Describe)
			.AssertNone();
	}

	private static bool ViolatesAsyncNaming(MethodInfo method)
	{
		if (IsTask(method.ReturnType))
		{
			return !method.Name.EndsWith("Async", StringComparison.Ordinal);
		}

		if (IsAsyncStream(method.ReturnType))
		{
			return !method.Name.EndsWith("Async", StringComparison.Ordinal) && !method.Name.EndsWith("Await", StringComparison.Ordinal);
		}

		return false;
	}

	private static bool IsAsync(Type type) => IsTask(type) || IsAsyncStream(type);

	private static bool IsTask(Type type)
	{
		if (type == typeof(Task) || type == typeof(ValueTask))
		{
			return true;
		}

		if (!type.IsGenericType)
		{
			return false;
		}

		var definition = type.GetGenericTypeDefinition();
		return definition == typeof(Task<>) || definition == typeof(ValueTask<>);
	}

	private static bool IsAsyncStream(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);
}
