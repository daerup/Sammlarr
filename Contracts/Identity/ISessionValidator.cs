using Domain.Identity;

namespace Contracts.Identity;

public interface ISessionValidator
{
	Task<bool> IsCurrentAsync(UserId id, UserRole role, CancellationToken cancellationToken = default);

	Task<bool> IsValidAsync(UserId id, UserRole role, DateTimeOffset issuedAt, CancellationToken cancellationToken = default);
}
