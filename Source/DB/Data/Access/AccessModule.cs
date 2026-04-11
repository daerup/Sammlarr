using Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Access;

internal class AccessModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<ICredentialStore, CredentialStore>();
		services.AddScoped<IApiKeyStore, ApiKeyStore>();
	}
}
