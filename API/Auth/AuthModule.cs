using Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Auth;

internal class AuthModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IApiKeyFactory, ApiKeyFactory>();
		services.AddScoped<ICredentialFactory, CredentialFactory>();
	}
}
