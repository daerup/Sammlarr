namespace Web.Auth;

internal static class SetupClaim
{
	private const string CookieName = "sammlarr.setup";

	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

	internal static void Issue(HttpContext context, string setupCode)
	{
		var options = Options(context);
		options.MaxAge = Lifetime;
		context.Response.Cookies.Append(CookieName, setupCode, options);
	}

	internal static Optional<string> Redeem(HttpContext context)
	{
		var setupCode = context.Request.Cookies[CookieName];
		context.Response.Cookies.Delete(CookieName, Options(context));
		return Optional.Of(setupCode).When(code => code.Length > 0);
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
