using Microsoft.AspNetCore.Identity;
using Shared.DI;

namespace Web.Auth;

internal sealed class AuthenticationModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddCascadingAuthenticationState();
		services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
		services.ConfigureApplicationCookie(options =>
		{
			options.Cookie.Name = "sammlarr.auth";
			options.LoginPath = "/signin";
			options.ReturnUrlParameter = "returnUrl";
			options.ExpireTimeSpan = TimeSpan.FromDays(30);
			options.SlidingExpiration = true;
		});
		services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
		services.AddAuthorization();
	}
}
