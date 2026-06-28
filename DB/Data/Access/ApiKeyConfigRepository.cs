using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Metadata;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal class ApiKeyConfigRepository(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	ITypedMemoryCache<MediaProvider, string> cache,
	IMediaRepository mediaRepository
) : IApiKeyConfigRepository
{
	public async ValueTask<string?> GetApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(provider, out var cached))
		{
			return cached;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var apiKey = await ctx.ApiKeySettings.Where(s => s.Provider == provider).Select(s => (string?)s.ApiKey).FirstOrDefaultAsync(cancellationToken);
		if (apiKey is not null)
		{
			cache.Set(provider, apiKey);
		}
		return apiKey;
	}

	public async Task SetApiKeyAsync(MediaProvider provider, string apiKey, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var setting = await ctx.ApiKeySettings.FirstOrDefaultAsync(s => s.Provider == provider, cancellationToken);
		if (setting is null)
		{
			ctx.ApiKeySettings.Add(new ApiKeyConfigEntity { Provider = provider, ApiKey = apiKey });
		}
		else
		{
			setting.ApiKey = apiKey;
		}

		await ctx.SaveChangesAsync(cancellationToken);
		cache.Set(provider, apiKey);
	}

	public async Task RemoveApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default)
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
}
