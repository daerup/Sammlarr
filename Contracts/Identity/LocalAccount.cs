using Domain.Identity;

namespace Contracts.Identity;

public sealed record LocalAccount(User User, string Username, Optional<DateTimeOffset> LockedUntil);
