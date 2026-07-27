using Domain.Identity;
using Domain.Providers;

namespace Contracts.Identity;

public interface IUserRepository
{
	Task<User> GetAsync(UserId id, CancellationToken cancellationToken = default);

	Task<Optional<User>> FindByLoginAsync(AuthProvider provider, string externalUserId, CancellationToken cancellationToken = default);

	Task CreateAsync(User user, AuthProvider provider, string externalUserId, CancellationToken cancellationToken = default);

	Task<bool> AnyAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<UserId>> GetAllIdsAsync(CancellationToken cancellationToken = default);
}
