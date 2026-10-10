namespace Domain.Media;

public sealed record FoundMedia(ValueSet<IExternalMediaId> Externals, MediaDetails Details)
{
	public bool Fits(MediaType type) => Details is not UnknownDetails && Details.Type == type && Externals.All(id => id.Type == type);

	public MatchedMedia Matched(MediaIdentity identity) => new(identity with { Keys = identity.Keys + Externals.OfType<IIdentityKey>() }, Details);

	public UnmatchedMedia Unmatched() => new(Externals, Details);

	public MediaMatchResult MatchTo(MediaIdentity identity) => Fits(identity.Type) ? Matched(identity) : Unmatched();
}
