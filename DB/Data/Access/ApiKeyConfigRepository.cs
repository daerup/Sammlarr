using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using Domain.Auth;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal class ApiKeyConfigRepository(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	ITypedMemoryCache<ApiKeyProvider, IApiKey> cache,
	IMediaRepository mediaRepository,
	IApiKeyFactory apiKeyFactory
) : IApiKeyConfigRepository
{
	public async Task<Result<IApiKey>> GetApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(provider, out var cached) && cached is not null)
		{
			return cached;
		}

		var stored = await LoadStoredKeyAsync(provider, cancellationToken);
		if (stored is null)
		{
			return Error.From("No API key is configured for this provider.");
		}

		var result = await apiKeyFactory.CreateAsync(provider, stored, cancellationToken);
		if (result is IApiKey apiKey)
		{
			cache.Set(provider, apiKey);
		}
		return result;
	}

	public async Task SetApiKeyAsync(IApiKey apiKey, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var setting = await ctx.ApiKeySettings.FirstOrDefaultAsync(s => s.Provider == apiKey.Provider, cancellationToken);
		if (setting is null)
		{
			ctx.ApiKeySettings.Add(new ApiKeyConfigRow { Provider = apiKey.Provider, ApiKey = apiKey.Value });
		}
		else
		{
			setting.ApiKey = apiKey.Value;
		}

		await ctx.SaveChangesAsync(cancellationToken);
		cache.Set(apiKey.Provider, apiKey);
	}

	public async Task RemoveApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var deleted = await ctx.ApiKeySettings.Where(s => s.Provider == provider).ExecuteDeleteAsync(cancellationToken);
		if (deleted == 0)
		{
			return;
		}

		cache.Remove(provider);

		if (provider.AsRatingSource() is { } ratingSource)
		{
			await mediaRepository.RemoveRatingsBySourceAsync(ratingSource, cancellationToken);
		}
	}

	private async Task<string?> LoadStoredKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.ApiKeySettings.Where(s => s.Provider == provider).Select(s => (string?)s.ApiKey).FirstOrDefaultAsync(cancellationToken);
	}
}
