using System.Runtime.CompilerServices;

namespace Domain.Tests;

internal static class VerifyConfig
{
	[ModuleInitializer]
	public static void Initialize() =>
		DerivePathInfo((_, projectDirectory, type, method) => new PathInfo(Path.Combine(projectDirectory, "Snapshots"), type.Name, method.Name));
}
