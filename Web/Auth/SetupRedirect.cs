using Domain.Providers;
using Microsoft.AspNetCore.WebUtilities;

namespace Web.Auth;

internal static class SetupRedirect
{
	internal static string For(Error error, AuthProvider signInWith)
	{
		return QueryHelpers.AddQueryString(
			"/setup",
			new Dictionary<string, string?>(StringComparer.Ordinal) { ["error"] = error.Reason, ["with"] = signInWith.ToString().ToLowerInvariant() }
		);
	}
}
