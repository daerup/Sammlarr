using Domain.Identity;

namespace Contracts.Setup;

public interface IOnboardingService
{
	Task<bool> NeedsAdminAsync(CancellationToken cancellationToken = default);

	Task<Optional<Error>> CodeProblemAsync(string setupCode, CancellationToken cancellationToken = default);

	Task<Result<User>> ClaimAsync(AdminClaim claim, CancellationToken cancellationToken = default);

	Task<bool> IsCompleteAsync(CancellationToken cancellationToken = default);

	Task CompleteAsync(CancellationToken cancellationToken = default);

	Task<bool> NeedsKeyPromptAsync(CancellationToken cancellationToken = default);
}
