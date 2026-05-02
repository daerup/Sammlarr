namespace Contracts.Processing;

public enum BypassReason
{
	Watchlisted,
	Blacklisted,
}

public readonly record struct Bypassed<T>(T Item, BypassReason Source, bool CountsTowardLimit);

public readonly record struct Truncated<T>(T Item);

public readonly record struct Culled<T>(T Item);

public union QueryResult<T>(T Item, Bypassed<T> bypassed, Truncated<T> truncated, Culled<T> culled);
