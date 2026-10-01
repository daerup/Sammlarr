using Contracts.Identity;
using Domain.Identity;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Identity;

internal sealed class UserRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IUserRepository
{
	public async Task<User> GetAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Users.AsNoTracking().Where(user => user.Id == id.Value).Select(Project).SingleAsync(cancellationToken);
	}

	public async Task<Optional<User>> FindByLoginAsync(AuthProvider provider, string externalUserId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var found = await ctx
			.Users.AsNoTracking()
			.Where(user => user.Provider == provider && user.ExternalUserId == externalUserId)
			.Select(Project)
			.SingleOrDefaultAsync(cancellationToken);

		return Optional.Of(found);
	}

	public async Task CreateAsync(User user, AuthProvider provider, string externalUserId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		ctx.Users.Add(
			new UserRow
			{
				Id = user.Id.Value,
				Provider = provider,
				ExternalUserId = externalUserId,
				DisplayName = user.DisplayName,
				AvatarUrl = user.AvatarUrl,
				Role = user.Role,
				JoinedAt = user.JoinedAt,
			}
		);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Users.AsNoTracking().OrderBy(user => user.JoinedAt).Select(Project).ToListAsync(cancellationToken);
	}

	public async Task ChangeRoleAsync(UserId id, UserRole role, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.Users.SingleAsync(user => user.Id == id.Value, cancellationToken);
		row.Role = role;
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.Users.SingleAsync(user => user.Id == id.Value, cancellationToken);
		ctx.Users.Remove(row);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static System.Linq.Expressions.Expression<Func<UserRow, User>> Project =>
		user => new User(UserId.From(user.Id), user.DisplayName, user.AvatarUrl, user.Role, user.JoinedAt);
}
