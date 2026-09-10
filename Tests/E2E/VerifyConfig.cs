using System.Runtime.CompilerServices;
using DiffEngine;

namespace E2E.Tests;

internal static class VerifyConfig
{
	[ModuleInitializer]
	internal static void Initialize() => DiffRunner.Disabled = true;
}
