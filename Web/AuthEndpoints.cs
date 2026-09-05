using System.Security.Claims;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;

namespace Web;

internal static class AuthEndpoints
{
	public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/auth/{provider}");

		group.MapGet(
			"/login",
			async (string provider, string? returnUrl, HttpContext context, ISignInService signIn, CancellationToken cancellationToken) =>
			{
				if (!TryParseProvider(provider, out var authProvider))
				{
					return Results.NotFound();
				}

				var callbackUrl = $"{context.Request.Scheme}://{context.Request.Host}/auth/{provider}/callback";
				if (!string.IsNullOrEmpty(returnUrl))
				{
					callbackUrl = QueryHelpers.AddQueryString(callbackUrl, "returnUrl", returnUrl);
				}

				return Results.Redirect(await signIn.BeginAsync(authProvider, callbackUrl, cancellationToken));
			}
		);

		group.MapGet(
			"/callback",
			async (string provider, string? returnUrl, HttpContext context, ISignInService signIn, CancellationToken cancellationToken) =>
			{
				if (!TryParseProvider(provider, out var authProvider))
				{
					return Results.NotFound();
				}

				return await signIn.CompleteAsync(authProvider, context.Request.QueryString.Value ?? string.Empty, cancellationToken) switch
				{
					Success<User>(var user) => await SignInAndRedirectAsync(context, user, returnUrl),
					Error error => Results.Redirect($"/signin?authError={Uri.EscapeDataString(error.Reason)}"),
				};
			}
		);

		endpoints.MapGet(
			"/auth/logout",
			async (HttpContext context) =>
			{
				await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
				return Results.Redirect("/");
			}
		);
	}

	private static async Task<IResult> SignInAndRedirectAsync(HttpContext context, User user, string? returnUrl)
	{
		await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, BuildPrincipal(user));
		return Results.Redirect(SafeLocalUrl(returnUrl));
	}

	private static string SafeLocalUrl(string? returnUrl) =>
		!string.IsNullOrWhiteSpace(returnUrl)
		&& Uri.TryCreate(returnUrl, UriKind.Relative, out _)
		&& returnUrl.StartsWith('/')
		&& !returnUrl.StartsWith("//", StringComparison.Ordinal)
			? returnUrl
			: "/";

	private static bool TryParseProvider(string provider, out AuthProvider authProvider) =>
		Enum.TryParse(provider, ignoreCase: true, out authProvider) && Enum.IsDefined(authProvider);

	private static ClaimsPrincipal BuildPrincipal(User user) =>
		new(
			new ClaimsIdentity(
				[
					new Claim(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
					new Claim(ClaimTypes.Name, user.DisplayName),
					new Claim(ClaimTypes.Role, user.Role.ToString()),
					new Claim(AuthClaims.Avatar, user.AvatarUrl),
				],
				CookieAuthenticationDefaults.AuthenticationScheme
			)
		);
}
