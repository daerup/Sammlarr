using Domain.Identity;

namespace Contracts.Auth;

public sealed record AccountLinkPreview(AccountLinkKind Kind, string DisplayName);

public interface IAccountLinkService
{
	Task<Optional<AccountLinkPreview>> InspectAsync(AccountLinkKind kind, UserId user, string token, CancellationToken cancellationToken = default);

	Task<Result<User>> JoinAsync(UserId user, string token, string password, string confirmation, CancellationToken cancellationToken = default);

	Task<Result<User>> ResetPasswordAsync(UserId user, string token, string password, string confirmation, CancellationToken cancellationToken = default);
}
