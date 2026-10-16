namespace Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalPages);
