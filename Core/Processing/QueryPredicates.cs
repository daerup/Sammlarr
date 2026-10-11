namespace Core.Processing;

internal sealed record QueryPredicates<T>(Func<T, bool> IsWantlisted, Func<T, bool> IsBlocklisted, Func<T, bool> IsOwned, Func<T, Guid> Identity);
