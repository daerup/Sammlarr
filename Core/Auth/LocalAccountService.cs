using Contracts;
using Contracts.Identity;
using Domain.Identity;
using Shared.Options;

namespace Core.Auth;

internal sealed class LocalAccountService(
	ILocalAccountRepository accounts,
	ICurrentUser currentUser,
	IDateTimeProvider dateTimeProvider,
	ISessionInvalidator sessionInvalidator,
	AccountsOptions options
) : ILocalAccountService
{
	public async Task<Result<User>> ChangePasswordAsync(string current, string next, CancellationToken cancellationToken = default)
	{
		var found = await accounts.GetAsync(currentUser.Id, cancellationToken);
		return await found.Match(
			account => ChangeAsync(account, current, next, cancellationToken),
			() => Error.From("Only a Sammlarr account has a password to change.")
		);
	}

	private async Task<Result<User>> ChangeAsync(LocalAccount account, string current, string next, CancellationToken cancellationToken)
	{
		if (!account.PasswordHash.Match(hash => Passwords.Verify(hash, current), () => false))
		{
			return Error.From("The current password is not right.");
		}

		return await Passwords.Weakness(next, options.MinimumPasswordLength).Match(weak => weak, () => StoreAsync(account, next, cancellationToken));
	}

	private async Task<Result<User>> StoreAsync(LocalAccount account, string next, CancellationToken cancellationToken)
	{
		await accounts.SetPasswordAsync(account.User.Id, Passwords.Hash(next), dateTimeProvider.Now, cancellationToken);
		sessionInvalidator.Invalidate(account.User.Id);
		return Success.Of(account.User);
	}
}
