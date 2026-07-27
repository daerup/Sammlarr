using Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Identity;

internal class IdentityDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IUserRepository, UserRepository>();
		services.AddScoped<IModelConfiguration, IdentityConfiguration>();
	}
}
