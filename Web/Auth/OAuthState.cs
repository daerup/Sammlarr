using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Web.Auth;

internal static class OAuthState
{
	internal const string CookieName = "sammlarr.oauth";

	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

	internal static string Issue(HttpContext context)
	{
		var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
		var options = Options(context);
		options.MaxAge = Lifetime;
		context.Response.Cookies.Append(CookieName, state, options);
		return state;
	}

	internal static bool Redeem(HttpContext context, string? state)
	{
		var expected = context.Request.Cookies[CookieName] ?? string.Empty;
		context.Response.Cookies.Delete(CookieName, Options(context));
		return expected.Length > 0 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(state ?? string.Empty));
	}

	private static CookieOptions Options(HttpContext context)
	{
		return new CookieOptions
		{
			HttpOnly = true,
			IsEssential = true,
			Path = "/auth",
			SameSite = SameSiteMode.Lax,
			Secure = context.Request.IsHttps,
		};
	}
}
