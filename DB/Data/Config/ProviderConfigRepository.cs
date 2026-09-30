using Contracts.Settings;
using Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal sealed class ProviderConfigRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IProviderConfigRepository
{
	public async ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.SingleOrDefaultAsync(cancellationToken) ?? ProviderConfigRow.Default;
		return new ProviderConfig
		{
			Sink = row.Sink,
			TrendingProvider = row.TrendingProvider,
			SearchProvider = row.SearchProvider,
			RatingProvider = row.RatingProvider,
			AuthProvider = row.AuthProvider,
			RegistrationMode = row.RegistrationMode,
		};
	}

	public async Task StoreAsync(ProviderConfig config, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.SingleOrDefaultAsync(cancellationToken);
		if (row is null)
		{
			ctx.ProviderSettings.Add(
				new ProviderConfigRow
				{
					Sink = config.Sink,
					TrendingProvider = config.TrendingProvider,
					SearchProvider = config.SearchProvider,
					RatingProvider = config.RatingProvider,
					AuthProvider = config.AuthProvider,
					RegistrationMode = config.RegistrationMode,
				}
			);
		}
		else
		{
			row.Sink = config.Sink;
			row.TrendingProvider = config.TrendingProvider;
			row.SearchProvider = config.SearchProvider;
			row.RatingProvider = config.RatingProvider;
			row.AuthProvider = config.AuthProvider;
			row.RegistrationMode = config.RegistrationMode;
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}
}
