using Domain.Auth;

namespace Contracts.Auth;

public sealed record PendingAdmin(Guid Id, string DisplayName, string AvatarUrl, HomeRole Home);
