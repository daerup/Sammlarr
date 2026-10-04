using Domain.Identity;

namespace Contracts.Auth;

public sealed record AccountLinkPreview(AccountLinkKind Kind, string DisplayName, string IssuedBy);

public interface IAccountLinkService
{
	Task<Optional<AccountLinkPreview>> InspectAsync(AccountLinkKind kind, string token, CancellationToken cancellationToken = default);

	Task<Result<User>> JoinAsync(string token, string password, string confirmation, CancellationToken cancellationToken = default);

	Task<Result<User>> ResetPasswordAsync(string token, string password, string confirmation, CancellationToken cancellationToken = default);
}
