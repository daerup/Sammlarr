using Domain.Media.Metadata;

namespace Domain.Media;

public sealed record IdentifiedMedia(MediaIdentity Identity, MediaDetails Details)
{
	public MediaField ConfirmedAbsent { get; init; }
}
