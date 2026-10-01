using Contracts.Auth;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal sealed class ApiKeyConfigRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IApiKeyConfigRepository
{
	public async Task<Result<string>> GetAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx
			.ApiKeySettings.SingleResultAsync(stored => stored.Provider == provider, "No API key is configured for this provider.", cancellationToken)
			.Map(stored => stored.ApiKey);
	}

	public async Task StoreAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var setting = await ctx.ApiKeySettings.FirstOrDefaultAsync(s => s.Provider == provider, cancellationToken);
		if (setting is null)
		{
			ctx.ApiKeySettings.Add(new ApiKeyConfigRow { Provider = provider, ApiKey = value });
		}
		else
		{
			setting.ApiKey = value;
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task ClearAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.ApiKeySettings.Where(s => s.Provider == provider).ExecuteDeleteAsync(cancellationToken);
	}
}
