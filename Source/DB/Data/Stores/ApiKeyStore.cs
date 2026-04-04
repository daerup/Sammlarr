using Contracts.Auth;
using Contracts.Media;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DB.Data.Stores;

internal class ApiKeyStore(SammlarrDbContext context, IMemoryCache cache) : IApiKeyStore
{
	public async Task<string?> GetApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(provider, out string? cached))
		{
			return cached;
		}

		var apiKey = await context.ApiKeySettings.Where(s => s.Provider == provider).Select(s => (string?)s.ApiKey).FirstOrDefaultAsync(cancellationToken);

		cache.Set(provider, apiKey);

		return apiKey;
	}

	public async Task SetApiKeyAsync(MediaProvider provider, string apiKey, CancellationToken cancellationToken = default)
	{
		var setting = await context.ApiKeySettings.FirstOrDefaultAsync(s => s.Provider == provider, cancellationToken);
		if (setting is null)
		{
			context.ApiKeySettings.Add(new ApiKeySetting { Provider = provider, ApiKey = apiKey });
		}
		else
		{
			setting.ApiKey = apiKey;
		}

		await context.SaveChangesAsync(cancellationToken);

		cache.Remove(provider);
	}

	public async Task ClearApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default)
	{
		var setting = await context.ApiKeySettings.FirstOrDefaultAsync(s => s.Provider == provider, cancellationToken);
		if (setting is null)
		{
			return;
		}

		context.ApiKeySettings.Remove(setting);
		await context.SaveChangesAsync(cancellationToken);

		cache.Remove(provider);
	}
}
