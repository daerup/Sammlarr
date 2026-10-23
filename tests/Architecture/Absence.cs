using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;
using Assembly = System.Reflection.Assembly;

namespace Architecture.Tests;

internal sealed record NullableMember(Type Type, MemberInfo Member, string Kind, string Parameter)
{
	internal string Namespace => Type.Namespace ?? string.Empty;

	internal string Describe() => $"{Type.FullName}.{Member.Name}{(Parameter.Length > 0 ? $"({Parameter})" : string.Empty)} [{Kind}]";
}

internal static class Absence
{
	internal const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

	private static readonly Assembly[] Scanned = [.. Projects.All.Except([Projects.Ui]), Projects.Messaging];

	internal static IEnumerable<NullableMember> NullableMembers => Scanned.SelectMany(assembly => assembly.GetTypes()).Where(IsScanned).SelectMany(MembersOf);

	private static IEnumerable<NullableMember> MembersOf(Type type)
	{
		if (type.IsDefined(typeof(GeneratedCodeAttribute), false))
		{
			yield break;
		}

		var context = new NullabilityInfoContext();
		var foreign = ForeignImplementations(type);
		foreach (var method in type.GetMethods(Declared).Where(method => !method.IsPrivate && (!method.IsSpecialName || method.Name.StartsWith("op_"))))
		{
			if (IsSkipped(method, foreign.Contains(method), method.GetBaseDefinition().DeclaringType != type))
			{
				continue;
			}

			if (IsNullable(context.Create(method.ReturnParameter), method.ReturnParameter.GetCustomAttributesData(), method))
			{
				yield return new NullableMember(type, method, "return", string.Empty);
			}

			foreach (var parameter in method.GetParameters().Where(parameter => IsNullable(context.Create(parameter), parameter.GetCustomAttributesData(), method)))
			{
				yield return new NullableMember(type, method, "parameter", parameter.Name!);
			}
		}

		foreach (var constructor in type.GetConstructors(Declared).Where(constructor => !constructor.IsPrivate && !IsSkipped(constructor, false, false)))
		{
			foreach (
				var parameter in constructor.GetParameters().Where(parameter => IsNullable(context.Create(parameter), parameter.GetCustomAttributesData(), constructor))
			)
			{
				yield return new NullableMember(type, constructor, "constructor", parameter.Name!);
			}
		}

		foreach (var property in type.GetProperties(Declared))
		{
			var accessors = property.GetAccessors(true);
			if (
				accessors.All(accessor => accessor.IsPrivate)
				|| IsSkipped(property, accessors.Any(foreign.Contains), accessors[0].GetBaseDefinition().DeclaringType != type)
			)
			{
				continue;
			}

			if (IsNullable(context.Create(property), property.GetCustomAttributesData(), property))
			{
				yield return new NullableMember(type, property, "property", string.Empty);
			}
		}

		foreach (var field in type.GetFields(Declared).Where(field => !type.IsEnum && !field.IsPrivate && !field.IsLiteral && !IsSkipped(field, false, false)))
		{
			if (IsNullable(context.Create(field), field.GetCustomAttributesData(), field))
			{
				yield return new NullableMember(type, field, "field", string.Empty);
			}
		}
	}

	private static bool IsScanned(Type type)
	{
		for (var current = type; current is not null; current = current.DeclaringType)
		{
			if (current.Name.Contains('<') || current.IsDefined(typeof(CompilerGeneratedAttribute), false) || current.IsNestedPrivate)
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsSkipped(MemberInfo member, bool implementsForeign, bool overrides)
	{
		return member.IsDefined(typeof(CompilerGeneratedAttribute), false)
			|| member.IsDefined(typeof(GeneratedCodeAttribute), false)
			|| overrides
			|| implementsForeign;
	}

	private static HashSet<MethodInfo> ForeignImplementations(Type type)
	{
		if (type.IsInterface)
		{
			return [];
		}

		return
		[
			.. type.GetInterfaces()
				.Where(contract => !Scanned.Contains(contract.Assembly))
				.SelectMany(contract => type.GetInterfaceMap(contract).TargetMethods)
				.Where(target => target.DeclaringType == type),
		];
	}

	private static bool IsNullable(NullabilityInfo info, IList<CustomAttributeData> attributes, MemberInfo member)
	{
		if (IsNullableAt(info, () => Annotation(attributes, member, 0)))
		{
			return true;
		}

		if (IsTask(info.Type) && IsNullableAt(info.GenericTypeArguments[0], () => Annotation(attributes, member, 1)))
		{
			return true;
		}

		return IsNestedNullable(info);
	}

	private static bool IsNullableAt(NullabilityInfo info, Func<byte?> declared)
	{
		var type = info.Type.IsByRef ? info.Type.GetElementType()! : info.Type;
		if (type.IsGenericParameter)
		{
			return declared() == 2;
		}

		return info.ReadState == NullabilityState.Nullable || info.WriteState == NullabilityState.Nullable;
	}

	private static bool IsNestedNullable(NullabilityInfo info)
	{
		return info
			.GenericTypeArguments.Append(info.ElementType)
			.OfType<NullabilityInfo>()
			.Any(argument => (!argument.Type.IsGenericParameter && argument.ReadState == NullabilityState.Nullable) || IsNestedNullable(argument));
	}

	private static bool IsTask(Type type)
	{
		return type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>));
	}

	private static byte? Annotation(IList<CustomAttributeData> attributes, MemberInfo member, int index)
	{
		var nullable = attributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
		if (nullable is not null)
		{
			return nullable.ConstructorArguments[0].Value switch
			{
				byte flag => flag,
				IReadOnlyList<CustomAttributeTypedArgument> flags when index < flags.Count => (byte)flags[index].Value!,
				_ => null,
			};
		}

		for (var scope = member; scope is not null; scope = scope.DeclaringType)
		{
			var context = scope
				.GetCustomAttributesData()
				.FirstOrDefault(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
			if (context is not null)
			{
				return (byte)context.ConstructorArguments[0].Value!;
			}
		}

		return null;
	}
}
