using Contracts.Auth;
using Contracts.Media;
using DB.Data.Stores;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data;

internal class DataAccessModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddMemoryCache();
		services.AddScoped<ICredentialStore, CredentialStore>();
		services.AddScoped<IApiKeyStore, ApiKeyStore>();
		services.AddScoped<IMediaIdMappingStore, MediaIdMappingMappingStore>();
	}
}
