using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal class ApiKeyConfigRepository(SammlarrDbContext context, ITypedMemoryCache<MediaProvider, string> cache) : IApiKeyConfigRepository
{
	public async ValueTask<string?> GetApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(provider, out var cached))
		{
			return cached;
		}

		var apiKey = await context.ApiKeySettings.Where(s => s.Provider == provider).Select(s => (string?)s.ApiKey).FirstOrDefaultAsync(cancellationToken);
		if (apiKey is not null)
		{
			cache.Set(provider, apiKey);
		}
		return apiKey;
	}

	public async Task SetApiKeyAsync(MediaProvider provider, string apiKey, CancellationToken cancellationToken = default)
	{
		var setting = await context.ApiKeySettings.FirstOrDefaultAsync(s => s.Provider == provider, cancellationToken);
		if (setting is null)
		{
			context.ApiKeySettings.Add(new ApiKeyConfigEntity { Provider = provider, ApiKey = apiKey });
		}
		else
		{
			setting.ApiKey = apiKey;
		}

		await context.SaveChangesAsync(cancellationToken);
		cache.Set(provider, apiKey);
	}

	public async Task RemoveApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default)
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
