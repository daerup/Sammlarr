using Contracts.Blacklist;
using Domain.Blacklist;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blacklist;

internal sealed class BlacklistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IBlacklistRepository
{
	public async Task<IReadOnlyList<BlacklistEntry>> GetBlacklistedEntriesAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.Blacklist.AsNoTracking()
			.Select(blacklist => new { MediaIdValue = blacklist.Media.Id, blacklist.CreatedAt })
			.ToListAsync(cancellationToken);
		return [.. rows.Select(row => new BlacklistEntry(MediaId.From(row.MediaIdValue), row.CreatedAt))];
	}

	public async Task AddAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var raw = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		if (await ctx.Blacklist.AnyAsync(b => b.Media.Id == raw, cancellationToken))
		{
			return;
		}

		var media = await ctx.Media.SingleOrDefaultAsync(m => m.Id == raw, cancellationToken) ?? throw new UnknownMediaException(mediaId);
		ctx.Blacklist.Add(new BlacklistRow { Media = media });
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var raw = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.Blacklist.Where(b => b.Media.Id == raw).ExecuteDeleteAsync(cancellationToken);
	}
}
