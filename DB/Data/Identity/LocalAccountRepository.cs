using Contracts.Identity;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Data.Identity;

internal sealed class LocalAccountRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, IServiceScopeFactory scopes) : ILocalAccountRepository
{
	public async Task<Optional<LocalAccount>> FindAsync(string username, CancellationToken cancellationToken = default)
	{
		var normalized = Usernames.Normalize(username);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var found = await Locals(ctx).AsNoTracking().SingleOrDefaultAsync(member => member.Row.UserName == normalized, cancellationToken);
		return Optional.Of(found).Map(ToAccount);
	}

	public async Task<Optional<LocalAccount>> GetAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var found = await Locals(ctx).AsNoTracking().SingleOrDefaultAsync(member => member.Row.Id == id.Value, cancellationToken);
		return Optional.Of(found).Map(ToAccount);
	}

	public async Task<IReadOnlyList<LocalAccount>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var members = await Locals(ctx).AsNoTracking().OrderBy(member => member.Row.JoinedAt).ToListAsync(cancellationToken);
		return [.. members.Select(ToAccount)];
	}

	public async Task<Result<User>> CreateAsync(User user, string username, string password, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = new UserRow
		{
			Id = user.Id.Value,
			UserName = Usernames.Normalize(username),
			DisplayName = user.DisplayName,
			TagName = user.TagName,
			AvatarUrl = user.AvatarUrl,
			JoinedAt = user.JoinedAt,
		};
		var created = await users.CreateAsync(row, password);
		if (created.Succeeded)
		{
			(await users.AddToRoleAsync(row, user.Role.ToString())).Ensure();
		}

		return created.ToResult(user);
	}

	public async Task<PasswordAttempt> CheckPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var signIns = scope.ServiceProvider.GetRequiredService<SignInManager<UserRow>>();
		var row = await signIns.UserManager.FindByNameAsync(Usernames.Normalize(username));
		if (row?.PasswordHash is null)
		{
			return new PasswordRejected();
		}

		var result = await signIns.CheckPasswordSignInAsync(row, password, lockoutOnFailure: true);
		if (result.IsLockedOut)
		{
			return new AccountLockedOut();
		}

		if (!result.Succeeded)
		{
			return new PasswordRejected();
		}

		return new PasswordAccepted(Members.ToUser(row, await RoleOfAsync(signIns.UserManager, row)));
	}

	public async Task<Result<User>> ChangePasswordAsync(UserId id, string current, string next, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = await users.RequireAsync(id);
		var changed = await users.ChangePasswordAsync(row, current, next);
		return changed.ToResult(Members.ToUser(row, await RoleOfAsync(users, row)));
	}

	public async Task<Result<User>> ReplacePasswordAsync(UserId id, string password, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = await users.RequireAsync(id);
		var replaced = await ReplaceAsync(users, row, password);
		return replaced.ToResult(Members.ToUser(row, await RoleOfAsync(users, row)));
	}

	public async Task<Result<ConfigReset>> ResetFromConfigAsync(UserId id, string password, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = await users.RequireAsync(id);
		if (row.ConfigResetHash is { } applied && users.PasswordHasher.VerifyHashedPassword(row, applied, password) != PasswordVerificationResult.Failed)
		{
			return Success.Of(ConfigReset.AlreadyApplied);
		}

		var replaced = await ReplaceAsync(users, row, password);
		if (replaced.Succeeded)
		{
			row.ConfigResetHash = users.PasswordHasher.HashPassword(row, password);
			(await users.UpdateAsync(row)).Ensure();
		}

		return replaced.ToResult(ConfigReset.Applied);
	}

	public async Task UnlockAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = await users.RequireAsync(id);
		await UnlockAsync(users, row);
	}

	private static async Task<IdentityResult> ReplaceAsync(UserManager<UserRow> users, UserRow row, string password)
	{
		var reset = await users.ResetPasswordAsync(row, await users.GeneratePasswordResetTokenAsync(row), password);
		if (reset.Succeeded)
		{
			await UnlockAsync(users, row);
		}

		return reset;
	}

	private static async Task UnlockAsync(UserManager<UserRow> users, UserRow row)
	{
		(await users.SetLockoutEndDateAsync(row, null)).Ensure();
		(await users.ResetAccessFailedCountAsync(row)).Ensure();
	}

	private static async Task<string> RoleOfAsync(UserManager<UserRow> users, UserRow row) => (await users.GetRolesAsync(row)).Single();

	private static IQueryable<Member> Locals(SammlarrDbContext ctx) => Members.Of(ctx).Where(member => member.Row.PasswordHash != null);

	private static LocalAccount ToAccount(Member member) => new(Members.ToUser(member), member.Row.UserName!, Optional.Of(member.Row.LockoutEnd));
}
