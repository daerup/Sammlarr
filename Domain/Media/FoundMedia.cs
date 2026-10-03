namespace Domain.Media;

public sealed record FoundMedia(ValueSet<IExternalMediaId> Externals, MediaDetails Details)
{
	public bool Fits(MediaType type) => Details is not UnknownDetails && Details.Type == type && Externals.All(id => id.Type == type);

	public UnmatchedMedia Unmatched() => new(Externals, Details);

	public MediaLookup LookUpAs(MediaIdentity identity) => Fits(identity.Type) ? IdentifiedAs(identity) : Unmatched();

	private IdentifiedMedia IdentifiedAs(MediaIdentity identity) => new(identity with { Externals = identity.Externals + Externals }, Details);
}
