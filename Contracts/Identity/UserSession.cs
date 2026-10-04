using Domain.Identity;

namespace Contracts.Identity;

public sealed record UserSession(UserRole Role, Optional<DateTimeOffset> RevokedAt);
