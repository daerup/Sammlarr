using System.Runtime.CompilerServices;

namespace Domain.Tests;

internal static class VerifyConfig
{
	[ModuleInitializer]
	public static void Initialize()
	{
		DerivePathInfo((_, projectDirectory, type, method) => new PathInfo(Path.Combine(projectDirectory, "Snapshots"), type.Name, method.Name));
		VerifierSettings.AddExtraSettings(settings => settings.Converters.Add(new OptionalConverter()));
	}
}

file sealed class OptionalConverter : WriteOnlyJsonConverter
{
	public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>);

	public override void Write(VerifyJsonWriter writer, object value)
	{
		var present = ((IUnion)value).Value!;
		var inner = present.GetType().GetProperty(nameof(Some<>.Value))?.GetValue(present);
		writer.Serialize(inner ?? nameof(None));
	}
}
