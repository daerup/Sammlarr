namespace Domain.Media;

public enum UnmatchedReason
{
	NotFound,
	CouldNotLookUp,
}

public sealed record UnmatchedMedia(ValueSet<IExternalMediaId> Externals, MediaDetails Details, UnmatchedReason Reason = UnmatchedReason.NotFound);
