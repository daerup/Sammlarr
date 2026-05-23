using Contracts.Media;
using Contracts.Media.Blacklist;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blacklist;

internal class BlacklistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IBlacklistRepository
{
	public async Task<IReadOnlyList<BlacklistedMedia>> GetBlacklistedEntriesAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from externalId in ctx.ExternalMediaIds.AsNoTracking().WhereSource(mediaProvider)
			join blacklist in ctx.Blacklist.AsNoTracking() on externalId.Media.Id equals blacklist.Media.Id
			select new { ExternalId = externalId, blacklist.CreatedAt }
		).ToListAsync(cancellationToken);
		return [.. rows.Select(row => new BlacklistedMedia(row.ExternalId.ToMediaId(), row.CreatedAt))];
	}

	public async Task<bool> IsBlacklistedAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await (from media in ctx.MediaMatching(mediaId) join blacklist in ctx.Blacklist on media.Id equals blacklist.Media.Id select 1).AnyAsync(
			cancellationToken
		);
	}

	public async Task AddAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var media = await ctx.MediaMatching(mediaId).SingleAsync(cancellationToken);

		var alreadyBlacklisted = await ctx.Blacklist.AnyAsync(b => b.Media.Id == media.Id, cancellationToken);
		if (alreadyBlacklisted)
		{
			return;
		}

		ctx.Blacklist.Add(new BlacklistEntity { Media = media });
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var mediaIdValue = await ctx.MediaMatching(mediaId).Select(m => m.Id).SingleAsync(cancellationToken);

		var entry = await ctx.Blacklist.SingleOrDefaultAsync(b => b.Media.Id == mediaIdValue, cancellationToken);
		if (entry is null)
		{
			return;
		}

		ctx.Blacklist.Remove(entry);
		await ctx.SaveChangesAsync(cancellationToken);
	}
}
