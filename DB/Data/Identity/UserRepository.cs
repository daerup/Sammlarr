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
		return await ctx
			.Users.AsNoTracking()
			.Where(user => user.Id == id.Value)
			.Select(user => new User(UserId.From(user.Id), user.DisplayName, user.AvatarUrl, user.Role))
			.SingleAsync(cancellationToken);
	}

	public async Task<Optional<User>> FindByLoginAsync(AuthProvider provider, string externalUserId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var found = await ctx
			.Users.AsNoTracking()
			.Where(user => user.Provider == provider && user.ExternalUserId == externalUserId)
			.Select(user => new User(UserId.From(user.Id), user.DisplayName, user.AvatarUrl, user.Role))
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
			}
		);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Users.AnyAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserId>> GetAllIdsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var ids = await ctx.Users.AsNoTracking().Select(user => user.Id).ToListAsync(cancellationToken);
		return ids.Select(UserId.From).ToList();
	}
}
