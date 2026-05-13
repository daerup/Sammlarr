using Contracts;
using Contracts.Media.Metadata;
using Contracts.Settings;
using DB.Data.MediaGroup;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Cleanup;

internal class MediaCachePurger(IDbContextFactory<SammlarrDbContext> dbContextFactory, IProviderConfigRepository providerConfigRepository) : IMediaCachePurger
{
	public async Task PurgeAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var anchor = config.WatchlistProvider.AsMediaProvider();

		var protectedGroupIds = await ctx.BlacklistEntries.Select(b => b.Media.Id).ToListAsync(cancellationToken);

		await ctx.MediaMetadata.ExecuteDeleteAsync(cancellationToken);
		await ctx.MediaIdGroups.Where(g => !protectedGroupIds.Contains(g.Id)).ExecuteDeleteAsync(cancellationToken);

		var protectedEntries = await ctx.MediaIdEntries.Where(e => protectedGroupIds.Contains(e.MediaIdGroupEntity.Id)).ToListAsync(cancellationToken);
		var nonAnchorIdentifiers = protectedEntries.Where(e => e.Identifier.Source != anchor).Select(e => e.Identifier).ToList();
		await ctx.MediaIdEntries.Where(MediaIdentifierGroupEntryExpressions.MatchesAnyIdentifier(nonAnchorIdentifiers)).ExecuteDeleteAsync(cancellationToken);
	}
}
