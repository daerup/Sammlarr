using Domain.Media;

namespace Contracts.Media;

public interface IIdentityClaims
{
	Task<ClaimOutcome> ClaimAsync(IdentityClaim claim, CancellationToken cancellationToken = default);
}
