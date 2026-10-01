using Contracts.Blocklist;
using Domain.Blocklist;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blocklist;

internal sealed class BlocklistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IBlocklistRepository
{
	public async Task<IReadOnlyList<BlocklistItem>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.Blocklist.AsNoTracking()
			.Select(blocklist => new { MediaIdValue = blocklist.Media.Id, blocklist.CreatedAt })
			.ToListAsync(cancellationToken);
		return [.. rows.Select(row => new BlocklistItem(MediaId.From(row.MediaIdValue), row.CreatedAt))];
	}

	public async Task AddAsync(MediaId id, CancellationToken cancellationToken = default)
	{
		var raw = id.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		if (await ctx.Blocklist.AnyAsync(b => b.Media.Id == raw, cancellationToken))
		{
			return;
		}

		var media = await ctx.Media.SingleOrDefaultAsync(m => m.Id == raw, cancellationToken) ?? throw new UnknownMediaException(id);
		ctx.Blocklist.Add(new BlocklistRow { Media = media });
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(MediaId id, CancellationToken cancellationToken = default)
	{
		var raw = id.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.Blocklist.Where(b => b.Media.Id == raw).ExecuteDeleteAsync(cancellationToken);
	}
}
