using Domain.Providers;

namespace UI.Shared.Services;

internal static class ServiceStatus
{
	internal static string Of(Access access, bool connected)
	{
		return access switch
		{
			UserSignIn => connected ? "Your account" : "Not connected",
			UserKey => connected ? "Your key is set" : "No key yet",
			AppInstance => connected ? "Connected" : "Not set up",
			Open => "Ready",
		};
	}
}
