using Domain.Identity;

namespace Contracts.Identity;

public interface IAccountLinkRepository
{
	Task StoreAsync(AccountLink link, string tokenHash, CancellationToken cancellationToken = default);

	Task<Optional<AccountLink>> FindAsync(string tokenHash, CancellationToken cancellationToken = default);

	Task<bool> ConsumeAsync(string tokenHash, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<AccountLink>> GetAllAsync(AccountLinkKind kind, CancellationToken cancellationToken = default);

	Task<bool> RevokeAsync(AccountLinkKind kind, string username, CancellationToken cancellationToken = default);
}
