using Contracts.Auth;
using Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace DB.Data.Identity;

internal sealed class IdentityDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IUserRepository, UserRepository>();
		services.AddScoped<ILocalAccountRepository, LocalAccountRepository>();
		services.AddScoped<ISignInCookie, IdentityCookie>();
		services.AddAuthenticationCore();
		services
			.AddIdentityCore<UserRow>()
			.AddRoles<IdentityRole<Guid>>()
			.AddEntityFrameworkStores<SammlarrDbContext>()
			.AddSignInManager()
			.AddDefaultTokenProviders()
			.AddClaimsPrincipalFactory<AccountClaims>()
			.AddErrorDescriber<AccountErrors>();
		services.AddOptions<IdentityOptions>().Configure<AccountsOptions>(Configure);
	}

	private static void Configure(IdentityOptions identity, AccountsOptions accounts)
	{
		identity.Password.RequiredLength = accounts.MinimumPasswordLength;
		identity.Password.RequireDigit = false;
		identity.Password.RequireLowercase = false;
		identity.Password.RequireUppercase = false;
		identity.Password.RequireNonAlphanumeric = false;
		identity.User.AllowedUserNameCharacters = string.Empty;
	}
}
