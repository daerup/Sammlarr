using Domain.Identity;

namespace Contracts.Identity;

public interface ILocalAccountRepository
{
	Task<Optional<LocalAccount>> FindAsync(string username, CancellationToken cancellationToken = default);

	Task<Optional<LocalAccount>> GetAsync(UserId id, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<LocalAccount>> GetAllAsync(CancellationToken cancellationToken = default);

	Task CreateAsync(User user, string username, string passwordHash, CancellationToken cancellationToken = default);

	Task SetPasswordAsync(UserId id, string passwordHash, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

	Task ResetFromConfigAsync(UserId id, string passwordHash, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

	Task SetFailedSignInsAsync(UserId id, int failures, Optional<DateTimeOffset> lockedUntil, CancellationToken cancellationToken = default);
}
