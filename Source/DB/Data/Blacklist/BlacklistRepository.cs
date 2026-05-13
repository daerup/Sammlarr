using Contracts.Media;
using Contracts.Media.Blacklist;
using DB.Data.MediaGroup;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blacklist;

internal class BlacklistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IBlacklistRepository
{
	public async Task<IReadOnlyList<BlacklistEntry>> GetBlacklistedEntriesAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entries = await ctx.BlacklistEntries.AsNoTracking().Include(b => b.Media).ThenInclude(m => m.MediaIdEntries).ToListAsync(cancellationToken);
		return entries
			.Select(b => (Entry: b.Media.MediaIdEntries.SingleOrDefault(e => e.Identifier.Source == mediaProvider), b.CreatedAt))
			.Select(x => new BlacklistEntry(x.Entry!.Identifier.ToMediaId(), x.CreatedAt))
			.ToList();
	}

	public async Task<bool> IsBlacklistedAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx
			.MediaIdEntries.Where(entry => entry.Identifier == identifier)
			.AnyAsync(entry => ctx.BlacklistEntries.Any(b => b.Media.Id == entry.MediaIdGroupEntity.Id), cancellationToken);
	}

	public async Task AddAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var mediaGroup = await ctx
			.MediaIdEntries.Where(entry => entry.Identifier == identifier)
			.Select(entry => entry.MediaIdGroupEntity)
			.SingleAsync(cancellationToken);

		var alreadyBlacklisted = await ctx.BlacklistEntries.AnyAsync(b => b.Media.Id == mediaGroup.Id, cancellationToken);
		if (alreadyBlacklisted)
		{
			return;
		}

		await ctx.BlacklistEntries.AddAsync(new BlacklistEntryEntity { Media = mediaGroup }, cancellationToken);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var mediaGroupId = await ctx
			.MediaIdEntries.Where(entry => entry.Identifier == identifier)
			.Select(entry => entry.MediaIdGroupEntity.Id)
			.SingleAsync(cancellationToken);

		var entry = await ctx.BlacklistEntries.SingleOrDefaultAsync(b => b.Media.Id == mediaGroupId, cancellationToken);
		if (entry is null)
		{
			return;
		}

		ctx.BlacklistEntries.Remove(entry);
		await ctx.SaveChangesAsync(cancellationToken);
	}
}
