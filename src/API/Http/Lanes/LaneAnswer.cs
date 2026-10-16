namespace API.Http.Lanes;

internal readonly union LaneAnswer<TOut>(LaneHit<TOut>, LaneMiss, LaneFailed) where TOut : notnull;

internal sealed record LaneHit<TOut>(TOut Value) where TOut : notnull;

internal sealed record LaneMiss;

internal sealed record LaneFailed(Error Cause);
