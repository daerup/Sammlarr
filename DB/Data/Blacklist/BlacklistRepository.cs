using Contracts.Blacklist;
using Contracts.Media;
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
		if (await ctx.Blacklist.AnyAsync(b => b.Media.Id == raw, cancellationToken))
		{
			return;
		}

		var media = await ctx.Media.SingleAsync(m => m.Id == raw, cancellationToken);
		ctx.Blacklist.Add(new BlacklistEntity { Media = media });
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var raw = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.Blacklist.Where(b => b.Media.Id == raw).ExecuteDeleteAsync(cancellationToken);
	}
}
