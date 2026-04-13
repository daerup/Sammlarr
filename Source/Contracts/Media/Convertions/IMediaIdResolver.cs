using Contracts;

namespace Contracts.Media.Convertions;

public interface IMediaIdResolver
{
	Task<ConversionResult> ResolveAsync(IReadOnlyCollection<IMediaId> candidates, MediaProvider target, Option<ResolutionHints> hints);
}
