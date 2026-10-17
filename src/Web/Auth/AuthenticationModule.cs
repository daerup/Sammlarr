using Microsoft.AspNetCore.Identity;
using Shared.DI;
using Shared.Options;

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
			options.AccessDeniedPath = "/";
			options.ReturnUrlParameter = "returnUrl";
			options.ExpireTimeSpan = TimeSpan.FromDays(30);
			options.SlidingExpiration = true;
			options.Events.OnValidatePrincipal = async context =>
			{
				await SecurityStampValidator.ValidatePrincipalAsync(context);
				context.ShouldRenew = false;
			};
		});
		services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
		services.AddOptions<SammlarOptions>().Validate(PublicOrigin.IsValid, PublicOrigin.Requirement).ValidateOnStart();
		services.AddAuthorization();
	}
}
