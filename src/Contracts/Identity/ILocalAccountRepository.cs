using Domain.Identity;

namespace Contracts.Identity;

public interface ILocalAccountRepository
{
	Task<Optional<LocalAccount>> FindAsync(string username, CancellationToken cancellationToken = default);

	Task<Optional<LocalAccount>> GetAsync(UserId id, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<LocalAccount>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<InvitedAccount>> GetInvitedAsync(CancellationToken cancellationToken = default);

	Task<Result<User>> CreateAsync(User user, string username, string password, CancellationToken cancellationToken = default);

	Task<Result<UserId>> InviteAsync(string username, CancellationToken cancellationToken = default);

	Task<PasswordAttempt> CheckPasswordAsync(string username, string password, CancellationToken cancellationToken = default);

	Task<Result<User>> ChangePasswordAsync(UserId id, string current, string next, CancellationToken cancellationToken = default);

	Task<IssuedToken> IssueTokenAsync(UserId id, CancellationToken cancellationToken = default);

	Task RevokeTokensAsync(UserId id, CancellationToken cancellationToken = default);

	Task<Optional<string>> LinkHolderAsync(AccountLinkKind kind, UserId id, string token, CancellationToken cancellationToken = default);

	Task<Result<User>> JoinAsync(UserId id, string token, string password, DateTimeOffset joinedAt, CancellationToken cancellationToken = default);

	Task<Result<User>> ResetPasswordAsync(UserId id, string token, string password, CancellationToken cancellationToken = default);

	Task<Result<ConfigReset>> ResetFromConfigAsync(UserId id, string password, CancellationToken cancellationToken = default);

	Task UnlockAsync(UserId id, CancellationToken cancellationToken = default);
}
