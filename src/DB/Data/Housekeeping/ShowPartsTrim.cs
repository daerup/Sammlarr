using Contracts.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Shared.Options;

namespace DB.Data.Housekeeping;

internal sealed class ShowPartsTrim(IDbContextFactory<SammlarrDbContext> contexts, HousekeepingOptions options) : IHousekeepingStep
{
	public HousekeepingStep Step => HousekeepingStep.ShowParts;

	public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		var askedBefore = (now - options.NoPrequelFor).ToUniversalTime();
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		return await ctx.ShowParts.Where(part => part.Media == null && part.AskedAt < askedBefore).ExecuteDeleteAsync(cancellationToken);
	}
}
