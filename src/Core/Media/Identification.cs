using Domain.Media;

namespace Core.Media;

internal readonly union Identification(IdentifyHandoff, Unidentified);

internal sealed record Unidentified(UnmatchedReason Reason, ValueSet<MediaId> ConflictsWith);
