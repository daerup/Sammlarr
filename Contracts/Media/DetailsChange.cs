using Domain.Media;

namespace Contracts.Media;

public sealed record DetailsChange(MediaId Id, Optional<CachedDetails> Before, CachedDetails After)
{
	public bool Changed => Before.Match(before => before != After, () => true);
}
