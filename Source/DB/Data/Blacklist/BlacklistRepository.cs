using Contracts.Media;
using Contracts.Media.Blacklist;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blacklist;

internal class BlacklistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IBlacklistRepository
{
	public async Task<IReadOnlyList<BlacklistedMedia>> GetBlacklistedEntriesAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.Blacklist.AsNoTracking()
			.Select(blacklist => new { MediaIdValue = blacklist.Media.Id, blacklist.CreatedAt })
			.ToListAsync(cancellationToken);
		return [.. rows.Select(row => new BlacklistedMedia(MediaId.From(row.MediaIdValue), row.CreatedAt))];
	}

	public async Task<bool> IsBlacklistedAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var raw = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Blacklist.AnyAsync(b => b.Media.Id == raw, cancellationToken);
	}

	public async Task AddAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var raw = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var media = await ctx.Media.SingleAsync(m => m.Id == raw, cancellationToken);

		var alreadyBlacklisted = await ctx.Blacklist.AnyAsync(b => b.Media.Id == raw, cancellationToken);
		if (alreadyBlacklisted)
		{
			return;
		}

		ctx.Blacklist.Add(new BlacklistEntity { Media = media });
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var raw = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entry = await ctx.Blacklist.SingleOrDefaultAsync(b => b.Media.Id == raw, cancellationToken);
		if (entry is null)
		{
			return;
		}

		ctx.Blacklist.Remove(entry);
		await ctx.SaveChangesAsync(cancellationToken);
	}
}
