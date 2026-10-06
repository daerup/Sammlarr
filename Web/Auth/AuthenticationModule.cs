using System.Threading.RateLimiting;
using Contracts.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Shared.DI;

namespace Web.Auth;

internal sealed class AuthenticationModule : IServiceModule
{
	internal const string SignInLimit = "sign-in";

	public void RegisterServices(IServiceCollection services)
	{
		services.AddCascadingAuthenticationState();
		services
			.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
			.AddCookie(options =>
			{
				options.Cookie.Name = "sammlarr.auth";
				options.LoginPath = "/signin";
				options.ReturnUrlParameter = "returnUrl";
				options.ExpireTimeSpan = TimeSpan.FromDays(30);
				options.SlidingExpiration = true;
				options.Events.OnValidatePrincipal = async context =>
				{
					if (context.Principal?.Identity?.IsAuthenticated != true)
					{
						return;
					}

					var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
					var issuedAt = context.Properties.IssuedUtc.GetValueOrDefault();
					if (await SessionRevalidation.IsCurrentAsync(context.Principal, issuedAt, validator, context.HttpContext.RequestAborted))
					{
						return;
					}

					context.RejectPrincipal();
					await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
				};
			});
		services.AddAuthorization();
		services.AddRateLimiter(options =>
		{
			options.AddPolicy(
				SignInLimit,
				context =>
					RateLimitPartition.GetFixedWindowLimiter(
						context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
						_ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }
					)
			);
			options.OnRejected = (context, _) =>
			{
				context.HttpContext.Response.Redirect(
					QueryHelpers.AddQueryString("/signin", "authError", "Too many sign-in attempts from here. Wait a minute and try again.")
				);
				return ValueTask.CompletedTask;
			};
		});
	}
}
