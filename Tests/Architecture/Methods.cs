using System.Reflection;
using System.Runtime.CompilerServices;
using Assembly = System.Reflection.Assembly;

namespace Architecture.Tests;

internal static class Methods
{
	internal static IEnumerable<MethodInfo> Public =>
		Projects
			.All.Except([Projects.Ui])
			.SelectMany(TypesOf)
			.Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null && !type.Name.Contains('<'))
			.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
			.Where(method => !method.IsSpecialName)
			.Where(method => method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
			.Where(method => method.GetCustomAttributes(false).All(attribute => attribute.GetType().Name != "SuppressArchTestAttribute"))
			.Where(method => method.GetBaseDefinition().DeclaringType == method.DeclaringType);

	internal static string Describe(MethodInfo method) => $"{method.DeclaringType!.FullName}.{method.Name}";

	private static IEnumerable<Type> TypesOf(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException exception)
		{
			return exception.Types.Where(type => type is not null)!;
		}
	}
}
