namespace Domain.Media;

public enum UnmatchedReason
{
	NotFound,
	CouldNotLookUp,
	NotFoundEarlier,
	Conflict,
}

public sealed record UnmatchedMedia(ValueSet<IExternalMediaId> Externals, MediaDetails Details, UnmatchedReason Reason = UnmatchedReason.NotFound)
{
	public ValueSet<MediaId> ConflictsWith { get; init; } = [];
}
