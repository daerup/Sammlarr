using Contracts.Identity;
using Domain.Identity;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Identity;

internal sealed class LocalAccountRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : ILocalAccountRepository
{
	public async Task<Optional<LocalAccount>> FindAsync(string username, CancellationToken cancellationToken = default)
	{
		var normalized = Usernames.Normalize(username);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await Local(ctx).AsNoTracking().SingleOrDefaultAsync(user => user.ExternalUserId == normalized, cancellationToken);
		return Optional.Of(row).Map(ToAccount);
	}

	public async Task<Optional<LocalAccount>> GetAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await Local(ctx).AsNoTracking().SingleOrDefaultAsync(user => user.Id == id.Value, cancellationToken);
		return Optional.Of(row).Map(ToAccount);
	}

	public async Task<IReadOnlyList<LocalAccount>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await Local(ctx).AsNoTracking().OrderBy(user => user.JoinedAt).ToListAsync(cancellationToken);
		return [.. rows.Select(ToAccount)];
	}

	public async Task CreateAsync(User user, string username, string passwordHash, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await UserHues.AddAsync(
			ctx,
			new UserRow
			{
				Id = user.Id.Value,
				Provider = AuthProvider.Local,
				ExternalUserId = Usernames.Normalize(username),
				DisplayName = user.DisplayName,
				TagName = user.TagName,
				AvatarUrl = user.AvatarUrl,
				Role = user.Role,
				JoinedAt = user.JoinedAt,
				PasswordHash = passwordHash,
			},
			cancellationToken
		);
	}

	public async Task SetPasswordAsync(UserId id, string passwordHash, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await Local(ctx).SingleAsync(user => user.Id == id.Value, cancellationToken);
		Store(row, passwordHash, revokedAt);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task ResetFromConfigAsync(UserId id, string passwordHash, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await Local(ctx).SingleAsync(user => user.Id == id.Value, cancellationToken);
		Store(row, passwordHash, revokedAt);
		row.ConfigResetHash = passwordHash;
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task SetFailedSignInsAsync(UserId id, int failures, Optional<DateTimeOffset> lockedUntil, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await Local(ctx).SingleAsync(user => user.Id == id.Value, cancellationToken);
		row.FailedSignIns = failures;
		row.LockedUntil = lockedUntil.ToNullable();
		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static IQueryable<UserRow> Local(SammlarrDbContext ctx) => ctx.Users.Where(user => user.Provider == AuthProvider.Local);

	private static void Store(UserRow row, string passwordHash, DateTimeOffset revokedAt)
	{
		row.PasswordHash = passwordHash;
		row.SessionsRevokedAt = revokedAt;
		row.FailedSignIns = 0;
		row.LockedUntil = null;
	}

	private static LocalAccount ToAccount(UserRow row)
	{
		return new LocalAccount(
			new User(UserId.From(row.Id), row.DisplayName, row.AvatarUrl, row.Role, row.JoinedAt) { TagName = row.TagName },
			row.ExternalUserId,
			Optional.Of(row.PasswordHash),
			row.FailedSignIns,
			Optional.Of(row.LockedUntil),
			Optional.Of(row.ConfigResetHash)
		);
	}
}
