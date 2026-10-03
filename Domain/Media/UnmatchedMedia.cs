namespace Domain.Media;

public sealed record UnmatchedMedia(ValueSet<IExternalMediaId> Externals, MediaDetails Details);
