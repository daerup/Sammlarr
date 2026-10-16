using Domain.Identity;

namespace Contracts.Identity;

public readonly union PasswordAttempt(PasswordAccepted, PasswordRejected, AccountLockedOut);

public readonly record struct PasswordAccepted(User User);

public readonly record struct PasswordRejected;

public readonly record struct AccountLockedOut;
