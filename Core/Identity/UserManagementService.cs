using Contracts;
using Contracts.Identity;
using Contracts.Settings;
using Contracts.Wantlist;
using Core.Auth;
using Core.Wantlist.Sync;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Domain.Usage;
using Observability.Usage;

namespace Core.Identity;

internal sealed class UserManagementService(
	IUserRepository users,
	ILocalAccountRepository accounts,
	IAccountLinkRepository links,
	ICurrentUser currentUser,
	IProviderConfigService providerConfig,
	ISessionValidator sessionValidator,
	ISessionInvalidator sessionInvalidator,
	ActiveSink sinks,
	IDateTimeProvider dateTimeProvider
) : IUserManagementService
{
	public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		return await users.GetAllAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		var now = dateTimeProvider.Now;
		var holdings = await users.GetHoldingsAsync(cancellationToken);
		var colours = await users.GetColoursAsync(cancellationToken);
		var locals = (await accounts.GetAllAsync(cancellationToken)).ToDictionary(account => account.User.Id);
		var invites = await links.GetAllAsync(AccountLinkKind.Invite, cancellationToken);
		var joined = (await users.GetAllAsync(cancellationToken)).Select(user =>
			(Person)
				new JoinedPerson(
					user,
					holdings.GetValueOrNone(user.Id).GetValueOr(PersonHoldings.Empty),
					LockOf(locals, user.Id, now),
					colours.GetValueOrNone(user.Id).GetValueOr(StoredColour.Empty)
				)
		);
		return [.. joined, .. invites.Select(invite => (Person)new InvitedPerson(invite.Username, invite.DisplayName, invite.ExpiresAt))];
	}

	public Task<int> CountAsync(CancellationToken cancellationToken = default) => users.CountAsync(cancellationToken);

	public async Task<IReadOnlyList<RosterColour>> GetColourRosterAsync(CancellationToken cancellationToken = default)
	{
		var colours = await users.GetColoursAsync(cancellationToken);
		var joined = await users.GetAllAsync(cancellationToken);
		return [.. joined.Select(user => new RosterColour(user.Id, user.JoinedAt, colours.GetValueOrNone(user.Id).GetValueOr(StoredColour.Empty)))];
	}

	public async Task<Result<UserRole>> ChangeRoleAsync(UserId id, UserRole role, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		var last = role == UserRole.Member && await IsLastAdminAsync(id, cancellationToken);
		return await Optional
			.When(last, () => Error.From("The last admin can't be made a member."))
			.Match(refused => refused, () => StoreRoleAsync(id, role, cancellationToken));
	}

	public async Task<Result<SinkHoldings>> SinkHoldingsAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		using var marked = UsageCauses.Mark(UsageCause.WantlistSync);
		var person = await users.GetAsync(id, cancellationToken);
		var sink = await sinks.ResolveAsync(cancellationToken);
		return await sink.HoldingsOfAsync(person, cancellationToken);
	}

	public async Task<Result<int>> DeleteAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		return await UnlessLastAdminAsync(id, () => RemoveAsync(id, 0, cancellationToken), cancellationToken);
	}

	public async Task<Result<int>> DeleteWithSinkTitlesAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		return await UnlessLastAdminAsync(id, () => ClearAndRemoveAsync(id, cancellationToken), cancellationToken);
	}

	public async Task SetRegistrationModeAsync(RegistrationMode mode, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		var config = await providerConfig.GetAsync(cancellationToken);
		await providerConfig.StoreAsync(config with { RegistrationMode = mode }, cancellationToken);
	}

	public async Task<Result<IssuedLink>> InviteAsync(string username, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		var name = username.Trim();
		var problem = await InviteProblemAsync(name, cancellationToken);
		return await problem.Match(refused => refused, () => IssueAsync(AccountLinkKind.Invite, name, name, new None(), cancellationToken));
	}

	public async Task<Result<IssuedLink>> RenewInviteAsync(string username, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		var normalized = Usernames.Normalize(username);
		var invite = (await links.GetAllAsync(AccountLinkKind.Invite, cancellationToken)).FirstOrNone(found => found.Username == normalized);
		return await invite.Match(
			found => IssueAsync(AccountLinkKind.Invite, found.Username, found.DisplayName, new None(), cancellationToken),
			() => Error.From($"{username} has no invite to renew.")
		);
	}

	public async Task RevokeInviteAsync(string username, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		await links.RevokeAsync(AccountLinkKind.Invite, username, cancellationToken);
	}

	public async Task<Result<IssuedLink>> ResetLinkAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		var account = await accounts.GetAsync(id, cancellationToken);
		return await account.Match(
			found => IssueAsync(AccountLinkKind.Reset, found.Username, found.User.DisplayName, Some.Of(id), cancellationToken),
			() => Error.From("Only a Sammlarr account has a password to reset.")
		);
	}

	public async Task UnlockAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		await accounts.SetFailedSignInsAsync(id, 0, new None(), cancellationToken);
	}

	public Task StoreColourAsync(UserId id, StoredColour colour, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		return users.StoreColourAsync(id, colour, cancellationToken);
	}

	private static Optional<DateTimeOffset> LockOf(IReadOnlyDictionary<UserId, LocalAccount> locals, UserId id, DateTimeOffset now)
	{
		return locals.GetValueOrNone(id).Match(account => account.LockedUntil.When(until => until > now), () => new None());
	}

	private async Task<Result<int>> RemoveAsync(UserId id, int cleared, CancellationToken cancellationToken)
	{
		await users.DeleteAsync(id, cancellationToken);
		sessionInvalidator.Invalidate(id);
		return Success.Of(cleared);
	}

	private async Task<Optional<Error>> InviteProblemAsync(string name, CancellationToken cancellationToken)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		var normalized = Usernames.Normalize(name);
		var taken =
			(await accounts.FindAsync(name, cancellationToken)).IsSome()
			|| (await links.GetAllAsync(AccountLinkKind.Invite, cancellationToken)).Any(invite =>
				invite.Username == normalized && invite.ExpiresAt > dateTimeProvider.Now
			);
		return Optional
			.When(config.AuthProvider != AuthProvider.Local, () => Error.From("Invites are for a Sammlarr that signs in with Sammlarr accounts."))
			.Or(() => Usernames.Problem(name))
			.Or(() => Optional.When(taken, () => Error.From($"{name} already has an account or an invite.")));
	}

	private async Task<Result<IssuedLink>> IssueAsync(
		AccountLinkKind kind,
		string username,
		string displayName,
		Optional<UserId> user,
		CancellationToken cancellationToken
	)
	{
		var token = LinkTokens.New();
		var expiresAt = dateTimeProvider.Now + LinkTokens.Lifetime;
		await links.StoreAsync(new AccountLink(kind, username, displayName, user, currentUser.DisplayName, expiresAt), LinkTokens.Hash(token), cancellationToken);
		return Success.Of(new IssuedLink(LinkPath(kind, token), expiresAt));
	}

	private static string LinkPath(AccountLinkKind kind, string token)
	{
		return kind switch
		{
			AccountLinkKind.Invite => $"/join/{token}",
			AccountLinkKind.Reset => $"/reset/{token}",
		};
	}

	private async Task EnsureCallerIsAdminAsync(CancellationToken cancellationToken)
	{
		if (await sessionValidator.IsCurrentAsync(currentUser.Id, UserRole.Admin, cancellationToken) is false)
		{
			throw new UnauthorizedAccessException("This action requires the Admin role.");
		}
	}

	private async Task<bool> IsLastAdminAsync(UserId id, CancellationToken cancellationToken)
	{
		var target = await users.GetAsync(id, cancellationToken);
		return target.Role == UserRole.Admin && (await users.GetAllAsync(cancellationToken)).Count(user => user.Role == UserRole.Admin) <= 1;
	}

	private async Task<Result<int>> UnlessLastAdminAsync(UserId id, Func<Task<Result<int>>> remove, CancellationToken cancellationToken)
	{
		var last = await IsLastAdminAsync(id, cancellationToken);
		return await Optional.When(last, () => Error.From("The last admin can't be removed.")).Match(refused => refused, remove);
	}

	private async Task<Result<int>> ClearAndRemoveAsync(UserId id, CancellationToken cancellationToken)
	{
		using var marked = UsageCauses.Mark(UsageCause.WantlistSync);
		var person = await users.GetAsync(id, cancellationToken);
		var sink = await sinks.ResolveAsync(cancellationToken);
		var cleared = await sink.DeleteTitlesOfAsync(person, cancellationToken);
		return await cleared.Map(count => RemoveAsync(id, count, cancellationToken));
	}

	private async Task<Result<UserRole>> StoreRoleAsync(UserId id, UserRole role, CancellationToken cancellationToken)
	{
		await users.ChangeRoleAsync(id, role, cancellationToken);
		sessionInvalidator.Invalidate(id);
		return Success.Of(role);
	}
}
