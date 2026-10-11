using Contracts.Housekeeping;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;
using Shared.Options;

namespace DB.Data.Housekeeping;

internal sealed class CachedDetailsTrim(IDbContextFactory<SammlarrDbContext> contexts, MediaWriteLock writeLock, HousekeepingOptions options)
	: IHousekeepingStep
{
	public HousekeepingStep Step => HousekeepingStep.CachedDetails;

	public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		var cutoff = (now - TimeSpan.FromDays(options.FreshDays + options.DetailsUnusedDays)).ToUniversalTime();
		using var held = await writeLock.AcquireAsync(cancellationToken);
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var unused = ctx.MediaDetails.Where(details => details.RefreshedAt < cutoff && !ctx.ProtectedMediaIds().Contains(details.MediaId));
		return await ctx.TrimDetailsAsync(unused, now, cancellationToken);
	}
}
