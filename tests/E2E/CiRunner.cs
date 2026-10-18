using System.Diagnostics;

namespace E2E.Tests;

public static class CiRunner
{
	internal const int Cores = 4;

	[Before(TestSession)]
	public static void UseOnlyItsCores()
	{
		if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
		{
			using var process = Process.GetCurrentProcess();
			process.ProcessorAffinity = (1 << Cores) - 1;
		}
	}
}
