using Contracts.Auth;
using Contracts.Caching;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal sealed class ApiKeyConfigRepository(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	ITypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey> cache,
	IApiKeyFactory apiKeyFactory,
	ICurrentUser currentUser
) : IApiKeyConfigRepository
{
	private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

	private (Guid User, ApiKeyProvider Provider) CacheKey(ApiKeyProvider provider) => (currentUser.IsAuthenticated ? currentUser.Id.Value : Guid.Empty, provider);

	public async ValueTask<Result<IApiKey>> GetApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey(provider), out var cached) && cached is not null)
		{
			return Success.Of(cached);
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx
			.ApiKeySettings.SingleResultAsync(stored => stored.Provider == provider, "No API key is configured for this provider.", cancellationToken)
			.Map(stored => apiKeyFactory.CreateAsync(provider, stored.ApiKey, cancellationToken))
			.Apply(apiKey => cache.Set(CacheKey(provider), apiKey, CacheLifetime));
	}

	public void Invalidate(ApiKeyProvider provider) => cache.Remove(CacheKey(provider));

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
		cache.Set(CacheKey(apiKey.Provider), apiKey, CacheLifetime);
	}

	public async Task RemoveApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var deleted = await ctx.ApiKeySettings.Where(s => s.Provider == provider).ExecuteDeleteAsync(cancellationToken);
		if (deleted == 0)
		{
			return;
		}

		cache.Remove(CacheKey(provider));
	}
}
