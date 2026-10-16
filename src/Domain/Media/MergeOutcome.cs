namespace Domain.Media;

public readonly union MergeOutcome(Merged, Contradicted, NotStored);

public sealed record Merged(MediaId Survivor, MediaId Removed);

public sealed record Contradicted(DistinctReason Reason);

public readonly record struct NotStored;
