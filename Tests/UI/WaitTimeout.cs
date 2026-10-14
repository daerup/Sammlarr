using Bunit;

namespace UI.Tests;

public static class WaitTimeout
{
	[Before(Assembly)]
	public static void AllowForABusyMachine() => BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(5);
}
