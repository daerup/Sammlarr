using Contracts.Wantlist;
using Domain.Identity;
using Domain.Settings;

namespace Contracts.Identity;

public interface IUserManagementService
{
	Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken cancellationToken = default);

	Task<int> CountAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<RosterColour>> GetColourRosterAsync(CancellationToken cancellationToken = default);

	Task<Result<UserRole>> ChangeRoleAsync(UserId id, UserRole role, CancellationToken cancellationToken = default);

	Task<Result<IReadOnlyList<SinkHoldings>>> SinkHoldingsAsync(UserId id, CancellationToken cancellationToken = default);

	Task<Result<int>> DeleteAsync(UserId id, CancellationToken cancellationToken = default);

	Task<Result<int>> DeleteWithSinkTitlesAsync(UserId id, CancellationToken cancellationToken = default);

	Task SetRegistrationModeAsync(RegistrationMode mode, CancellationToken cancellationToken = default);

	Task<Result<IssuedLink>> InviteAsync(string username, CancellationToken cancellationToken = default);

	Task<Result<IssuedLink>> RenewInviteAsync(string username, CancellationToken cancellationToken = default);

	Task RevokeInviteAsync(string username, CancellationToken cancellationToken = default);

	Task<Result<IssuedLink>> ResetLinkAsync(UserId id, CancellationToken cancellationToken = default);

	Task UnlockAsync(UserId id, CancellationToken cancellationToken = default);

	Task StoreColourAsync(UserId id, StoredColour colour, CancellationToken cancellationToken = default);
}
