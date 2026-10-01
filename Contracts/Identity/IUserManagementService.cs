using Domain.Identity;
using Domain.Settings;

namespace Contracts.Identity;

public interface IUserManagementService
{
	Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

	Task ChangeRoleAsync(UserId id, UserRole role, CancellationToken cancellationToken = default);

	Task DeleteAsync(UserId id, CancellationToken cancellationToken = default);

	Task SetRegistrationModeAsync(RegistrationMode mode, CancellationToken cancellationToken = default);
}
