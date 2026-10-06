using System.Security.Claims;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Setup;
using Domain.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using UI.Shared.Services;

namespace Web.Auth;

internal static class AuthEndpoints
{
	private const string SetupPath = "/setup";

	private static readonly Error StaleSignIn = Error.From("The sign-in timed out or was started in another browser. Please try again.");

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

				var callbackUrl = QueryHelpers.AddQueryString(
					$"{context.Request.Scheme}://{context.Request.Host}/auth/{provider}/callback",
					"state",
					OAuthState.Issue(context)
				);
				if (!string.IsNullOrEmpty(returnUrl))
				{
					callbackUrl = QueryHelpers.AddQueryString(callbackUrl, "returnUrl", returnUrl);
				}

				var started = await signIn.BeginAsync(authProvider, callbackUrl, cancellationToken);
				return started.Match(
					signInPage => Results.Redirect(signInPage),
					error => Results.Redirect(Failed(error, authProvider, SetupClaim.Redeem(context), returnUrl))
				);
			}
		);

		group.MapGet(
			"/callback",
			async (string provider, string? state, string? returnUrl, HttpContext context, ISignInService signIn, CancellationToken cancellationToken) =>
			{
				if (!TryParseProvider(provider, out var authProvider))
				{
					return Results.NotFound();
				}

				var setupCode = SetupClaim.Redeem(context);
				if (!OAuthState.Redeem(context, state))
				{
					return Results.Redirect(Failed(StaleSignIn, authProvider, setupCode, returnUrl));
				}

				var query = context.Request.QueryString.Value ?? string.Empty;
				return await SignedIn(context.User)
					.Match(
						user => signIn.LinkAsync(authProvider, query, user, cancellationToken),
						() =>
							setupCode.Match(
								code => signIn.CompleteSetupAsync(authProvider, query, code, cancellationToken),
								() => signIn.CompleteAsync(authProvider, query, cancellationToken)
							)
					)
					.Map(user => SignInAndRedirectAsync(context, user, returnUrl))
					.GetValueOr(error => Results.Redirect(Failed(error, authProvider, setupCode, returnUrl)));
			}
		);

		endpoints
			.MapPost(
				"/auth/local",
				async (
					[FromForm] string username,
					[FromForm] string password,
					[FromForm] string? returnUrl,
					HttpContext context,
					ISignInService signIn,
					CancellationToken cancellationToken
				) =>
				{
					return await signIn
						.SignInWithPasswordAsync(username, password, cancellationToken)
						.Map(user => SignInAndRedirectAsync(context, user, returnUrl))
						.GetValueOr(error => Results.Redirect(SignInPage(error, returnUrl)));
				}
			)
			.RequireRateLimiting(AuthenticationModule.SignInLimit);
		endpoints
			.MapPost(
				"/auth/join",
				async ([FromForm] string token, [FromForm] string password, [FromForm] string confirmation, HttpContext context, IAccountLinkService links) =>
				{
					return await links
						.JoinAsync(token, password, confirmation, context.RequestAborted)
						.Map(user => SignInAndRedirectAsync(context, user, "/"))
						.GetValueOr(error => Results.Redirect(LinkPage("join", token, error)));
				}
			)
			.RequireRateLimiting(AuthenticationModule.SignInLimit);
		endpoints
			.MapPost(
				"/auth/reset",
				async ([FromForm] string token, [FromForm] string password, [FromForm] string confirmation, HttpContext context, IAccountLinkService links) =>
				{
					return await links
						.ResetPasswordAsync(token, password, confirmation, context.RequestAborted)
						.Map(user => SignInAndRedirectAsync(context, user, "/"))
						.GetValueOr(error => Results.Redirect(LinkPage("reset", token, error)));
				}
			)
			.RequireRateLimiting(AuthenticationModule.SignInLimit);
		endpoints
			.MapPost(
				"/setup/admin",
				async (
					[FromForm] string code,
					[FromForm] string username,
					[FromForm] string password,
					[FromForm] string confirmation,
					HttpContext context,
					IOnboardingService onboarding
				) =>
				{
					return await onboarding
						.ClaimAsync(new AdminClaim(code, username, password, confirmation), context.RequestAborted)
						.Map(user => SignInAndRedirectAsync(context, user, SetupPath))
						.GetValueOr(error => Results.Redirect(SetupRedirect.For(error, AuthProvider.Local)));
				}
			)
			.RequireRateLimiting(AuthenticationModule.SignInLimit);
		endpoints
			.MapPost(
				"/setup/plex",
				async ([FromForm] string code, HttpContext context, IOnboardingService onboarding) =>
				{
					var problem = await onboarding.CodeProblemAsync(code, context.RequestAborted);
					return problem.Match(
						error => Results.Redirect(SetupRedirect.For(error, AuthProvider.Plex)),
						() =>
						{
							SetupClaim.Issue(context, code);
							return Results.Redirect(QueryHelpers.AddQueryString("/auth/plex/login", "returnUrl", SetupPath));
						}
					);
				}
			)
			.RequireRateLimiting(AuthenticationModule.SignInLimit);
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
		return Results.Redirect(ReturnUrls.Target(returnUrl));
	}

	private static bool TryParseProvider(string provider, out AuthProvider authProvider) =>
		Enum.TryParse(provider, ignoreCase: true, out authProvider) && Enum.IsDefined(authProvider) && authProvider != AuthProvider.Local;

	private static Optional<UserId> SignedIn(ClaimsPrincipal principal)
	{
		if (principal.Identity?.IsAuthenticated != true || !Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id))
		{
			return new None();
		}

		return Some.Of(UserId.From(id));
	}

	private static string LinkPage(string page, string token, Error error)
	{
		return QueryHelpers.AddQueryString($"/{page}/{Uri.EscapeDataString(token)}", "error", error.Reason);
	}

	private static string Failed(Error error, AuthProvider provider, Optional<string> setupCode, string? returnUrl)
	{
		return setupCode.Match(_ => SetupRedirect.For(error, provider), () => SignInPage(error, returnUrl));
	}

	private static string SignInPage(Error error, string? returnUrl)
	{
		var page = QueryHelpers.AddQueryString("/signin", "authError", error.Reason);
		return string.IsNullOrWhiteSpace(returnUrl) ? page : QueryHelpers.AddQueryString(page, "returnUrl", returnUrl);
	}

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
