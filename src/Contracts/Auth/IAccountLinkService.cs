using Domain.Identity;

namespace Contracts.Auth;

public sealed record AccountLinkPreview(AccountLinkKind Kind, string DisplayName);

public interface IAccountLinkService
{
	Task<Optional<AccountLinkPreview>> InspectAsync(AccountLinkKind kind, UserId user, Secret token, CancellationToken cancellationToken = default);

	Task<Result<User>> JoinAsync(UserId user, Secret token, Secret password, Secret confirmation, CancellationToken cancellationToken = default);

	Task<Result<User>> ResetPasswordAsync(UserId user, Secret token, Secret password, Secret confirmation, CancellationToken cancellationToken = default);
}
