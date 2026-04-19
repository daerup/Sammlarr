using Contracts.Media;
using Contracts.Media.Blacklist;
using DB.Data.MediaGroup;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blacklist;

internal class BlacklistRepository(SammlarrDbContext context) : IBlacklistRepository
{
	public async Task<IReadOnlyList<BlacklistEntry>> GetBlacklistedEntriesAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		var entries = await context.BlacklistEntries.AsNoTracking().Include(b => b.Media).ThenInclude(m => m.MediaIdEntries).ToListAsync(cancellationToken);
		return entries
			.Select(b => (Entry: b.Media.MediaIdEntries.SingleOrDefault(e => e.Identifier.Source == mediaProvider), b.CreatedAt))
			.Select(x => new BlacklistEntry(x.Entry!.Identifier.ToMediaId(), x.CreatedAt))
			.ToList();
	}

	public async Task<bool> IsBlacklistedAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		return await context
			.MediaIdEntries.Where(entry => entry.Identifier == identifier)
			.AnyAsync(entry => context.BlacklistEntries.Any(b => b.Media.Id == entry.MediaIdGroupEntity.Id), cancellationToken);
	}

	public async Task AddAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		var mediaGroup = await context
			.MediaIdEntries.Where(entry => entry.Identifier == identifier)
			.Select(entry => entry.MediaIdGroupEntity)
			.SingleAsync(cancellationToken);

		var alreadyBlacklisted = await context.BlacklistEntries.AnyAsync(b => b.Media.Id == mediaGroup.Id, cancellationToken);
		if (alreadyBlacklisted)
		{
			return;
		}

		await context.BlacklistEntries.AddAsync(new BlacklistEntryEntity { Media = mediaGroup }, cancellationToken);
		await context.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		var mediaGroupId = await context
			.MediaIdEntries.Where(entry => entry.Identifier == identifier)
			.Select(entry => entry.MediaIdGroupEntity.Id)
			.SingleAsync(cancellationToken);

		var entry = await context.BlacklistEntries.SingleOrDefaultAsync(b => b.Media.Id == mediaGroupId, cancellationToken);
		if (entry is null)
		{
			return;
		}

		context.BlacklistEntries.Remove(entry);
		await context.SaveChangesAsync(cancellationToken);
	}
}
