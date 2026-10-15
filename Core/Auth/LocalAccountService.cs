using Contracts.Identity;
using Domain.Identity;

namespace Core.Auth;

internal sealed class LocalAccountService(ILocalAccountRepository accounts, ICurrentUser currentUser, ISessionInvalidator sessionInvalidator)
	: ILocalAccountService
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
		var changed = await accounts.ChangePasswordAsync(account.User.Id, current, next, cancellationToken);
		changed.Apply(_ => sessionInvalidator.Invalidate(account.User.Id));
		return changed;
	}
}
