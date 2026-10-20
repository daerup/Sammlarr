using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Core.Notifications;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Core.Auth;

internal sealed class AccountLinkService(
	ILocalAccountRepository accounts,
	IProviderConfigService providerConfig,
	IUserScope userScope,
	ISessionInvalidator sessionInvalidator,
	TimeProvider clock,
	WebUiNotificationSeeder webUiNotifications,
	ILogger<AccountLinkService> logger
) : IAccountLinkService
{
	private static readonly Error Unusable = Error.From(UnusableLink.Message);

	public async Task<Optional<AccountLinkPreview>> InspectAsync(AccountLinkKind kind, UserId user, string token, CancellationToken cancellationToken = default)
	{
		if (!await IsLocalAsync(cancellationToken))
		{
			return new None();
		}

		var holder = await accounts.LinkHolderAsync(kind, user, token, cancellationToken);
		return holder.Map(name => new AccountLinkPreview(kind, name));
	}

	public async Task<Result<User>> JoinAsync(UserId user, string token, string password, string confirmation, CancellationToken cancellationToken = default)
	{
		var problem = await ProblemAsync(password, confirmation, cancellationToken);
		return await problem.Match(refused => refused, () => JoinedAsync(user, token, password, cancellationToken));
	}

	public async Task<Result<User>> ResetPasswordAsync(
		UserId user,
		string token,
		string password,
		string confirmation,
		CancellationToken cancellationToken = default
	)
	{
		var problem = await ProblemAsync(password, confirmation, cancellationToken);
		return await problem.Match(refused => refused, () => ResetAsync(user, token, password, cancellationToken));
	}

	private async Task<bool> IsLocalAsync(CancellationToken cancellationToken)
	{
		return (await providerConfig.GetAsync(cancellationToken)).AuthProvider == AuthProvider.Local;
	}

	private async Task<Optional<Error>> ProblemAsync(string password, string confirmation, CancellationToken cancellationToken)
	{
		var local = await IsLocalAsync(cancellationToken);
		return Optional.When(!local, () => Unusable).Or(() => Passwords.Mismatch(password, confirmation));
	}

	private async Task<Result<User>> JoinedAsync(UserId id, string token, string password, CancellationToken cancellationToken)
	{
		var joined = await accounts.JoinAsync(id, token, password, clock.GetUtcNow(), cancellationToken);
		return await joined.Map(user => WelcomeAsync(user, cancellationToken));
	}

	private async Task<Result<User>> WelcomeAsync(User user, CancellationToken cancellationToken)
	{
		using (userScope.As(user))
		{
			await webUiNotifications.SeedAsync(cancellationToken);
		}

		logger.LogInformation("Invite redeemed by new user {UserId}", user.Id);
		return Success.Of(user);
	}

	private async Task<Result<User>> ResetAsync(UserId id, string token, string password, CancellationToken cancellationToken)
	{
		var reset = await accounts.ResetPasswordAsync(id, token, password, cancellationToken);
		reset.Apply(_ => Announce(id));
		return reset;
	}

	private void Announce(UserId id)
	{
		sessionInvalidator.Invalidate(id);
		logger.LogInformation("Password reset through a link for user {UserId}", id);
	}
}
