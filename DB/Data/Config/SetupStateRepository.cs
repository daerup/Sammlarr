using Contracts.Setup;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal sealed class SetupStateRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : ISetupStateRepository
{
	public async Task<Optional<DateTimeOffset>> CompletedAtAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var completedAt = await ctx.ProviderSettings.Select(row => row.OnboardingCompletedAt).SingleOrDefaultAsync(cancellationToken);
		return Optional.Of(completedAt);
	}

	public async Task SetCompletedAtAsync(Optional<DateTimeOffset> completedAt, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var stamp = completedAt.ToNullable();
		await ctx.ProviderSettings.ExecuteUpdateAsync(rows => rows.SetProperty(row => row.OnboardingCompletedAt, stamp), cancellationToken);
	}
}
