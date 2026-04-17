namespace Contracts.Processing;

public readonly record struct Skipped<T>(T Item);

public readonly record struct Filtered<T>(T Item);

public union QueryResult<T>(T Item, Skipped<T> skipped, Filtered<T> filtered);
