using Contracts;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Core.Notifications;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace Core.Auth;

internal sealed class AccountLinkService(
	IAccountLinkRepository links,
	ILocalAccountRepository accounts,
	IProviderConfigService providerConfig,
	IUserScope userScope,
	ISessionInvalidator sessionInvalidator,
	IDateTimeProvider dateTimeProvider,
	WebUiNotificationSeeder webUiNotifications,
	AccountsOptions options,
	ILogger<AccountLinkService> logger
) : IAccountLinkService
{
	private static readonly Error Unusable = Error.From("This link has expired or was already used. Ask your admin for a new one.");

	public async Task<Optional<AccountLinkPreview>> InspectAsync(AccountLinkKind kind, string token, CancellationToken cancellationToken = default)
	{
		var found = await FindAsync(kind, token, cancellationToken);
		return found.Map(link => new AccountLinkPreview(link.Kind, link.DisplayName, link.IssuedBy));
	}

	public async Task<Result<User>> JoinAsync(string token, string password, string confirmation, CancellationToken cancellationToken = default)
	{
		var found = await FindAsync(AccountLinkKind.Invite, token, cancellationToken);
		return await found.Match(invite => Checked(password, confirmation, () => JoinAsync(invite, token, password, cancellationToken)), () => Unusable);
	}

	public async Task<Result<User>> ResetPasswordAsync(string token, string password, string confirmation, CancellationToken cancellationToken = default)
	{
		var found = await FindAsync(AccountLinkKind.Reset, token, cancellationToken);
		return await found.Match(reset => Checked(password, confirmation, () => ResetAsync(reset, token, password, cancellationToken)), () => Unusable);
	}

	private async Task<Optional<AccountLink>> FindAsync(AccountLinkKind kind, string token, CancellationToken cancellationToken)
	{
		var now = dateTimeProvider.Now;
		var local = (await providerConfig.GetAsync(cancellationToken)).AuthProvider == AuthProvider.Local;
		var found = await links.FindAsync(LinkTokens.Hash(token), cancellationToken);
		return found.When(link => local && link.Kind == kind && link.ExpiresAt > now);
	}

	private Task<Result<User>> Checked(string password, string confirmation, Func<Task<Result<User>>> next)
	{
		return Passwords.Problem(password, confirmation, options.MinimumPasswordLength).Match(problem => problem, next);
	}

	private async Task<Result<User>> JoinAsync(AccountLink invite, string token, string password, CancellationToken cancellationToken)
	{
		if (!await links.ConsumeAsync(LinkTokens.Hash(token), cancellationToken))
		{
			return Unusable;
		}

		var user = new User(UserId.New(), invite.DisplayName, string.Empty, UserRole.Member, dateTimeProvider.Now);
		await accounts.CreateAsync(user, invite.Username, Passwords.Hash(password), cancellationToken);
		using (userScope.As(user))
		{
			await webUiNotifications.SeedAsync(cancellationToken);
		}

		logger.LogInformation("Invite redeemed by new user {UserId}", user.Id);
		return Success.Of(user);
	}

	private async Task<Result<User>> ResetAsync(AccountLink reset, string token, string password, CancellationToken cancellationToken)
	{
		if (!await links.ConsumeAsync(LinkTokens.Hash(token), cancellationToken))
		{
			return Unusable;
		}

		return await reset.User.Match(id => StoreAsync(id, password, cancellationToken), () => Unusable);
	}

	private async Task<Result<User>> StoreAsync(UserId id, string password, CancellationToken cancellationToken)
	{
		await accounts.SetPasswordAsync(id, Passwords.Hash(password), dateTimeProvider.Now, cancellationToken);
		sessionInvalidator.Invalidate(id);
		var account = await accounts.GetAsync(id, cancellationToken);
		logger.LogInformation("Password reset through a link for user {UserId}", id);
		return account.Map(found => found.User).ToResult(Unusable);
	}
}
