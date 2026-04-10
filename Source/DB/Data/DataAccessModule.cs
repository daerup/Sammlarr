using Contracts.Auth;
using Contracts.Media;
using Contracts.Settings;
using DB.Data.Stores;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data;

internal class DataAccessModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<ICredentialStore, CredentialStore>();
		services.AddScoped<IApiKeyStore, ApiKeyStore>();
		services.AddScoped<IProviderSettingsStore, DB.Data.Stores.ProviderSettingsStore>();
		services.AddScoped<IMediaIdGroupStore, MediaIdGroupStore>();
	}
}
