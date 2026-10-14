using Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Auth;

internal class AuthDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<ICredentialRepository, CredentialRepository>();
		services.AddScoped<IApiKeyConfigRepository, ApiKeyConfigRepository>();
		services.AddScoped<IModelConfiguration, AuthEntityConfiguration>();
	}
}
