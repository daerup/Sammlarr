namespace Shared.Concurrency;

public union LaneAnswer<TOut>(LaneHit<TOut>, LaneMiss, LaneFailed) where TOut : notnull;

public sealed record LaneHit<TOut>(TOut Value) where TOut : notnull;

public sealed record LaneMiss;

public sealed record LaneFailed(Error Cause);
