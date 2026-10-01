using Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Auth;

internal class AuthModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped(typeof(SecretCache<,>));
		services.AddTracedScoped<ISignInService, SignInService>();
		services.AddTracedScoped<IApiKeyService, ApiKeyService>();
		services.AddTracedScoped<ICredentialService, CredentialService>();
	}
}
