namespace Web.Auth;

internal static class AdminSwapClaim
{
	private const string CookieName = "sammlarr.admin-swap";

	internal static void Issue(HttpContext context) => AuthCookie.Issue(context, CookieName, "1");

	internal static bool Redeem(HttpContext context) => AuthCookie.Redeem(context, CookieName).IsSome();
}
