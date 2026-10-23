using System.Buffers.Text;
using System.Text;
using Contracts.Identity;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

	public async Task<Result<User>> CreateAsync(User user, string username, Secret password, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var row = new UserRow
		{
			Id = user.Id.Value,
			UserName = Usernames.Normalize(username),
			DisplayName = user.DisplayName,
			TagName = user.TagName,
			AvatarUrl = user.AvatarUrl,
			JoinedAt = user.JoinedAt,
		};
		return await scope.InTransactionAsync(
			async users =>
			{
				var created = await users.CreateAsync(row, password.Reveal());
				if (created.Succeeded)
				{
					(await users.AddToRoleAsync(row, user.Role.ToString())).Ensure();
				}

				return created.ToResult(user);
			},
			cancellationToken
		);
	}

	public async Task<PasswordAttempt> CheckPasswordAsync(string username, Secret password, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var signIns = scope.ServiceProvider.GetRequiredService<SignInManager<UserRow>>();
		var row = await signIns.UserManager.FindByNameAsync(Usernames.Normalize(username));
		if (row?.PasswordHash is null)
		{
			return new PasswordRejected();
		}

		var result = await signIns.CheckPasswordSignInAsync(row, password.Reveal(), lockoutOnFailure: true);
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

	public async Task<Result<User>> ChangePasswordAsync(UserId id, Secret current, Secret next, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var signIns = scope.ServiceProvider.GetRequiredService<SignInManager<UserRow>>();
		var users = signIns.UserManager;
		var row = await users.RequireAsync(id);
		var check = await signIns.CheckPasswordSignInAsync(row, current.Reveal(), lockoutOnFailure: true);
		if (check.IsLockedOut)
		{
			return Error.From(Lockout.Message);
		}

		if (!check.Succeeded)
		{
			return Error.From(users.ErrorDescriber.PasswordMismatch().Description);
		}

		var changed = await users.ChangePasswordAsync(row, current.Reveal(), next.Reveal());
		return changed.ToResult(Members.ToUser(row, await RoleOfAsync(users, row)));
	}

	public async Task<IReadOnlyList<InvitedAccount>> GetInvitedAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.Users.AsNoTracking()
			.Where(user => user.JoinedAt == null)
			.OrderBy(user => user.CreatedAt)
			.Select(user => new
			{
				user.Id,
				user.UserName,
				user.DisplayName,
			})
			.ToListAsync(cancellationToken);
		return [.. rows.Select(row => new InvitedAccount(UserId.From(row.Id), row.UserName!, row.DisplayName))];
	}

	public async Task<Result<UserId>> InviteAsync(string username, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var name = username.Trim();
		var row = new UserRow
		{
			Id = UserId.New().Value,
			UserName = Usernames.Normalize(name),
			DisplayName = name,
			TagName = name,
			AvatarUrl = string.Empty,
		};
		return await scope.InTransactionAsync(
			async users =>
			{
				var created = await users.CreateAsync(row);
				if (created.Succeeded)
				{
					(await users.AddToRoleAsync(row, nameof(UserRole.Member))).Ensure();
				}

				return created.ToResult(UserId.From(row.Id));
			},
			cancellationToken
		);
	}

	public async Task<IssuedToken> IssueTokenAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var token = await users.GeneratePasswordResetTokenAsync(await users.RequireAsync(id));
		var lifetime = scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan;
		return new IssuedToken(new Secret(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token))), lifetime);
	}

	public async Task RevokeTokensAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		(await users.UpdateSecurityStampAsync(await users.RequireAsync(id))).Ensure();
	}

	public async Task<Optional<string>> LinkHolderAsync(AccountLinkKind kind, UserId id, Secret token, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = await HolderAsync(users, kind, id, token);
		if (row is null)
		{
			return new None();
		}

		var valid = await users.VerifyUserTokenAsync(
			row,
			users.Options.Tokens.PasswordResetTokenProvider,
			UserManager<UserRow>.ResetPasswordTokenPurpose,
			Decode(token)
		);
		return Optional.When(valid, () => row.DisplayName);
	}

	public async Task<Result<User>> JoinAsync(UserId id, Secret token, Secret password, DateTimeOffset joinedAt, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = await HolderAsync(users, AccountLinkKind.Invite, id, token);
		if (row is null)
		{
			return Unusable(users);
		}

		row.JoinedAt = joinedAt;
		var joined = await users.ResetPasswordAsync(row, Decode(token), password.Reveal());
		return joined.ToResult(Members.ToUser(row, await RoleOfAsync(users, row)));
	}

	public async Task<Result<User>> ResetPasswordAsync(UserId id, Secret token, Secret password, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		return await scope.InTransactionAsync(
			async users =>
			{
				var row = await HolderAsync(users, AccountLinkKind.Reset, id, token);
				if (row is null)
				{
					return Unusable(users);
				}

				var reset = await ReplaceAsync(users, row, Decode(token), password);
				return reset.ToResult(Members.ToUser(row, await RoleOfAsync(users, row)));
			},
			cancellationToken
		);
	}

	public async Task<Result<ConfigReset>> ResetFromConfigAsync(UserId id, Secret password, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = await users.RequireAsync(id);
		if (row.ConfigResetHash is { } applied && users.PasswordHasher.VerifyHashedPassword(row, applied, password.Reveal()) != PasswordVerificationResult.Failed)
		{
			return Success.Of(ConfigReset.AlreadyApplied);
		}

		var replaced = await ReplaceAsync(users, row, await users.GeneratePasswordResetTokenAsync(row), password);
		if (replaced.Succeeded)
		{
			row.ConfigResetHash = users.PasswordHasher.HashPassword(row, password.Reveal());
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

	private static bool Holds(AccountLinkKind kind, UserRow row)
	{
		return kind switch
		{
			AccountLinkKind.Invite => row.JoinedAt is null,
			AccountLinkKind.Reset => row.JoinedAt is not null && row.PasswordHash is not null,
		};
	}

	private static async Task<UserRow?> HolderAsync(UserManager<UserRow> users, AccountLinkKind kind, UserId id, Secret token)
	{
		var row = await users.FindByIdAsync(id.Value.ToString());
		return row is not null && Holds(kind, row) && Base64Url.IsValid(token.Reveal()) ? row : null;
	}

	private static Result<User> Unusable(UserManager<UserRow> users) => Error.From(users.ErrorDescriber.InvalidToken().Description);

	private static string Decode(Secret token) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token.Reveal()));

	private static async Task<IdentityResult> ReplaceAsync(UserManager<UserRow> users, UserRow row, string token, Secret password)
	{
		var reset = await users.ResetPasswordAsync(row, token, password.Reveal());
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
