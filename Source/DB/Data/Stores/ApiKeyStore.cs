using Contracts.Media;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Stores;

public sealed class ApiKeyStore(SammlarrDbContext context) : IApiKeyStore
{
	public Task<string?> GetApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default)
	{
		return context.ApiKeySettings.Where(s => s.Provider == provider).Select(s => (string?)s.ApiKey).FirstOrDefaultAsync(cancellationToken);
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
	}

	public async Task ClearApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default)
	{
		var setting = await context.ApiKeySettings.FirstOrDefaultAsync(s => s.Provider == provider, cancellationToken);
		if (setting is null)
			return;

		context.ApiKeySettings.Remove(setting);
		await context.SaveChangesAsync(cancellationToken);
	}
}
