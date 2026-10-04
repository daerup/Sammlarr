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
		await UserHues.AddAsync(
			ctx,
			new UserRow
			{
				Id = user.Id.Value,
				Provider = provider,
				ExternalUserId = externalUserId,
				DisplayName = user.DisplayName,
				AvatarUrl = user.AvatarUrl,
				Role = user.Role,
				JoinedAt = user.JoinedAt,
			},
			cancellationToken
		);
	}

	public async Task<IReadOnlyDictionary<UserId, double>> GetHuesAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Users.AsNoTracking().ToDictionaryAsync(user => UserId.From(user.Id), user => user.Hue, cancellationToken);
	}

	public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Users.AsNoTracking().OrderBy(user => user.JoinedAt).Select(Project).ToListAsync(cancellationToken);
	}

	public async Task<int> CountAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Users.CountAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<UserId, PersonHoldings>> GetHoldingsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var feeds = await CountedAsync(ctx.Feeds.IgnoreQueryFilters(), cancellationToken);
		var wanted = await CountedAsync(ctx.Wantlist.IgnoreQueryFilters(), cancellationToken);
		var blocked = await CountedAsync(ctx.Blocklist.IgnoreQueryFilters(), cancellationToken);
		var credentials = await ctx
			.Credentials.IgnoreQueryFilters()
			.Select(credential => new { Owner = EF.Property<Guid>(credential, UserOwnedConfigurationExtensions.UserIdColumn), credential.Provider })
			.ToListAsync(cancellationToken);
		var keys = await ctx
			.ApiKeySettings.IgnoreQueryFilters()
			.Select(key => new { Owner = EF.Property<Guid>(key, UserOwnedConfigurationExtensions.UserIdColumn), key.Provider })
			.ToListAsync(cancellationToken);
		var owners = await ctx.Users.AsNoTracking().Select(user => user.Id).ToListAsync(cancellationToken);
		return owners.ToDictionary(
			UserId.From,
			owner => new PersonHoldings(
				feeds.GetValueOrDefault(owner),
				wanted.GetValueOrDefault(owner),
				blocked.GetValueOrDefault(owner),
				[
					.. credentials.Where(credential => credential.Owner == owner).Select(credential => credential.Provider.AsMediaProvider()),
					.. keys.Where(key => key.Owner == owner).Select(key => key.Provider.AsMediaProvider()),
				]
			)
		);
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

	public async Task<Optional<UserSession>> FindSessionAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var found = await ctx
			.Users.AsNoTracking()
			.Where(user => user.Id == id.Value)
			.Select(user => new { user.Role, user.SessionsRevokedAt })
			.SingleOrDefaultAsync(cancellationToken);
		return Optional.Of(found).Map(session => new UserSession(session.Role, Optional.Of(session.SessionsRevokedAt)));
	}

	public async Task<bool> WasKeyPromptedAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Users.AsNoTracking().AnyAsync(user => user.Id == id.Value && user.KeyPromptedAt != null, cancellationToken);
	}

	public async Task MarkKeyPromptedAsync(UserId id, DateTimeOffset promptedAt, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var user = await ctx.Users.SingleAsync(row => row.Id == id.Value, cancellationToken);
		user.KeyPromptedAt = promptedAt;
		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static async Task<Dictionary<Guid, int>> CountedAsync<T>(IQueryable<T> rows, CancellationToken cancellationToken)
		where T : class
	{
		return await rows.GroupBy(row => EF.Property<Guid>(row, UserOwnedConfigurationExtensions.UserIdColumn))
			.Select(owned => new { Owner = owned.Key, Count = owned.Count() })
			.ToDictionaryAsync(owned => owned.Owner, owned => owned.Count, cancellationToken);
	}

	private static System.Linq.Expressions.Expression<Func<UserRow, User>> Project =>
		user => new User(UserId.From(user.Id), user.DisplayName, user.AvatarUrl, user.Role, user.JoinedAt);
}
