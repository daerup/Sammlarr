using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Web.Auth;

internal static class OAuthState
{
	internal const string CookieName = "sammlarr.oauth";

	internal static string Issue(HttpContext context)
	{
		var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
		AuthCookie.Issue(context, CookieName, state);
		return state;
	}

	internal static bool Redeem(HttpContext context, string? state)
	{
		var expected = AuthCookie.Redeem(context, CookieName).GetValueOr(string.Empty);
		return expected.Length > 0 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(state ?? string.Empty));
	}
}
