namespace Contracts.Processing;

public sealed record QueryPredicates<T>(Func<T, bool> IsWatchlisted, Func<T, bool> IsBlacklisted, Func<T, bool> IsOwned);
