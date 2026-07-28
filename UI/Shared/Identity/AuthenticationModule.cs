using Contracts.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Shared.DI;

namespace UI.Shared.Identity;

internal sealed class AuthenticationModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddCascadingAuthenticationState();
		services.AddSingleton<CircuitUserRegistry>();
		services.AddSingleton<ISessionInvalidator>(sp => sp.GetRequiredService<CircuitUserRegistry>());
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
					if (await SessionRevalidation.IsCurrentAsync(context.Principal, validator, context.HttpContext.RequestAborted))
					{
						return;
					}

					context.RejectPrincipal();
					await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
				};
			});
		services.AddAuthorization();
	}
}
