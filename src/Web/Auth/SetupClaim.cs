namespace Web.Auth;

internal static class SetupClaim
{
	private const string CookieName = "sammlarr.setup";

	internal static void Issue(HttpContext context, string setupCode) => AuthCookie.Issue(context, CookieName, setupCode);

	internal static Optional<string> Read(HttpContext context) => AuthCookie.Read(context, CookieName);

	internal static Optional<string> Redeem(HttpContext context) => AuthCookie.Redeem(context, CookieName);
}
