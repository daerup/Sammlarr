namespace Web.Auth;

internal static class PendingAdminCookie
{
	private const string CookieName = "sammlarr.pending-admin";

	internal static void Issue(HttpContext context, Guid id) => AuthCookie.Issue(context, CookieName, id.ToString());

	internal static Optional<Guid> Redeem(HttpContext context)
	{
		return AuthCookie.Redeem(context, CookieName).When(value => Guid.TryParse(value, out _)).Map(Guid.Parse);
	}
}
