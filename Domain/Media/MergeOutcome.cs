namespace Domain.Media;

public union MergeOutcome(Merged, Contradicted, NotStored);

public sealed record Merged(MediaId Survivor, MediaId Removed);

public sealed record Contradicted(DistinctReason Reason);

public readonly record struct NotStored;
