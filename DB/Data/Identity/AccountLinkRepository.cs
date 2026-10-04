using Contracts.Identity;
using Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Identity;

internal sealed class AccountLinkRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IAccountLinkRepository
{
	public async Task StoreAsync(AccountLink link, string tokenHash, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var username = Usernames.Normalize(link.Username);
		ctx.AccountLinks.RemoveRange(await ctx.AccountLinks.Where(row => row.Kind == link.Kind && row.Username == username).ToListAsync(cancellationToken));
		ctx.AccountLinks.Add(
			new AccountLinkRow
			{
				Id = Guid.NewGuid(),
				Kind = link.Kind,
				Username = username,
				DisplayName = link.DisplayName,
				UserId = link.User.Map(id => id.Value).ToNullable(),
				TokenHash = tokenHash,
				IssuedBy = link.IssuedBy,
				ExpiresAt = link.ExpiresAt,
			}
		);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task<Optional<AccountLink>> FindAsync(string tokenHash, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.AccountLinks.AsNoTracking().SingleOrDefaultAsync(link => link.TokenHash == tokenHash, cancellationToken);
		return Optional.Of(row).Map(ToLink);
	}

	public async Task<bool> ConsumeAsync(string tokenHash, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.AccountLinks.Where(link => link.TokenHash == tokenHash).ExecuteDeleteAsync(cancellationToken) == 1;
	}

	public async Task<IReadOnlyList<AccountLink>> GetAllAsync(AccountLinkKind kind, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.AccountLinks.AsNoTracking().Where(link => link.Kind == kind).OrderBy(link => link.CreatedAt).ToListAsync(cancellationToken);
		return [.. rows.Select(ToLink)];
	}

	public async Task<bool> RevokeAsync(AccountLinkKind kind, string username, CancellationToken cancellationToken = default)
	{
		var normalized = Usernames.Normalize(username);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.AccountLinks.Where(link => link.Kind == kind && link.Username == normalized).ExecuteDeleteAsync(cancellationToken) > 0;
	}

	private static AccountLink ToLink(AccountLinkRow row)
	{
		return new AccountLink(row.Kind, row.Username, row.DisplayName, Optional.Of(row.UserId).Map(UserId.From), row.IssuedBy, row.ExpiresAt);
	}
}
