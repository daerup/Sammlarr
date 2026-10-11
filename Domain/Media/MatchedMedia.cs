namespace Domain.Media;

public sealed record MatchedMedia(MediaIdentity Identity, MediaDetails Details)
{
	public bool Known { get; init; }

	public bool Stale { get; init; }
}
