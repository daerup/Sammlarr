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

	public async Task<IReadOnlyList<MediaId>> AddAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var raw = ids.Select(id => id.Value).Distinct().ToList();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var listed = await ctx.Blocklist.Where(b => raw.Contains(b.Media.Id)).Select(b => b.Media.Id).ToListAsync(cancellationToken);
		var adding = raw.Except(listed).ToList();
		var media = await ctx.Media.Where(m => adding.Contains(m.Id)).ToListAsync(cancellationToken);
		if (media.Count < adding.Count)
		{
			throw new UnknownMediaException(MediaId.From(adding.First(id => media.All(found => found.Id != id))));
		}

		ctx.Blocklist.AddRange(media.Select(found => new BlocklistRow { Media = found }));
		await ctx.SaveChangesAsync(cancellationToken);
		return [.. adding.Select(MediaId.From)];
	}

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var raw = ids.Select(id => id.Value).Distinct().ToList();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var removing = await ctx.Blocklist.Where(b => raw.Contains(b.Media.Id)).Select(b => b.Media.Id).ToListAsync(cancellationToken);
		await ctx.Blocklist.Where(b => removing.Contains(b.Media.Id)).ExecuteDeleteAsync(cancellationToken);
		return [.. removing.Select(MediaId.From)];
	}
}
