using Domain.Media;

namespace Core.Media;

internal union Identification(IdentifyHandoff, Unidentified);

internal sealed record Unidentified(UnmatchedReason Reason, ValueSet<MediaId> ConflictsWith);
