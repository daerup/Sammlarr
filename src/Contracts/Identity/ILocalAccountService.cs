using Domain.Identity;

namespace Contracts.Identity;

public interface ILocalAccountService
{
	Task<Result<User>> ChangePasswordAsync(Secret current, Secret next, CancellationToken cancellationToken = default);
}
