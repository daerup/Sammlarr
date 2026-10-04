using Domain.Identity;

namespace Contracts.Identity;

public sealed record LocalAccount(
	User User,
	string Username,
	Optional<string> PasswordHash,
	int FailedSignIns,
	Optional<DateTimeOffset> LockedUntil,
	Optional<string> ConfigResetHash
);
