using Contracts.Housekeeping;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;
using Shared.Options;

namespace DB.Data.Housekeeping;

internal sealed class TitleIdsTrim(IDbContextFactory<SammlarrDbContext> contexts, MediaWriteLock writeLock, HousekeepingOptions options) : IHousekeepingStep
{
	public HousekeepingStep Step => HousekeepingStep.TitleIds;

	public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		var trimmedBefore = (now - TimeSpan.FromDays(options.IdsUnusedDays - options.DetailsUnusedDays)).ToUniversalTime();
		var addedBefore = (now - TimeSpan.FromDays(options.IdsUnusedDays)).ToUniversalTime();
		using var held = await writeLock.AcquireAsync(cancellationToken);
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		return await ctx
			.Media.Where(media =>
				!ctx.MediaDetails.Any(details => details.MediaId == media.Id)
				&& !ctx.ProtectedMediaIds().Contains(media.Id)
				&& (media.DetailsTrimmedAt < trimmedBefore || (media.DetailsTrimmedAt == null && media.CreatedAt < addedBefore))
			)
			.ExecuteDeleteAsync(cancellationToken);
	}
}
