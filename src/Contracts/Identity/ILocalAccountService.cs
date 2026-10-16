using Domain.Identity;

namespace Contracts.Identity;

public interface ILocalAccountService
{
	Task<Result<User>> ChangePasswordAsync(string current, string next, CancellationToken cancellationToken = default);
}
