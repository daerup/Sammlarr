namespace Domain.Media;

public union ClaimOutcome(Claimed, Conflicted);

public sealed record Claimed(MediaId Id, ValueSet<IIdentityKey> Keys)
{
	public ValueSet<Disputed> Disputed { get; init; } = [];
}

public sealed record Conflicted(ValueSet<MediaId> Owners, ValueSet<IIdentityKey> Keys);

public sealed record Disputed(IIdentityKey Key, MediaId Owner);
