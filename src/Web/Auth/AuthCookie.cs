namespace Web.Auth;

internal static class AuthCookie
{
	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

	internal static void Issue(HttpContext context, string name, string value)
	{
		var options = Options(context);
		options.MaxAge = Lifetime;
		context.Response.Cookies.Append(name, value, options);
	}

	internal static Optional<string> Read(HttpContext context, string name) => Optional.Of(context.Request.Cookies[name]).When(value => value.Length > 0);

	internal static Optional<string> Redeem(HttpContext context, string name)
	{
		context.Response.Cookies.Delete(name, Options(context));
		return Read(context, name);
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
