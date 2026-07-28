using Domain.Identity;
using Domain.Settings;

namespace Contracts.Identity;

public interface IUserManagementService
{
	Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

	Task ChangeRoleAsync(UserId userId, UserRole role, CancellationToken cancellationToken = default);

	Task RemoveAsync(UserId userId, CancellationToken cancellationToken = default);

	ValueTask<RegistrationMode> GetRegistrationModeAsync(CancellationToken cancellationToken = default);

	Task SetRegistrationModeAsync(RegistrationMode mode, CancellationToken cancellationToken = default);
}
