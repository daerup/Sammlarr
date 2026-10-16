using Harness;

namespace Integration.Tests.TestHost;

public static class TestHostScratch
{
	[After(TestSession)]
	public static void Remove() => SammlarrTestHost.RemoveScratch();
}
