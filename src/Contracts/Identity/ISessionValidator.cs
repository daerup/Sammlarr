using Domain.Identity;

namespace Contracts.Identity;

public interface ISessionValidator
{
	Task<bool> IsCurrentAsync(UserId id, UserRole role, CancellationToken cancellationToken = default);
}
