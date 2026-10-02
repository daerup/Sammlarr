using Domain.Media;

namespace Contracts.Media.Conversions;

public readonly record struct IdLookupResult(IReadOnlyList<IExternalMediaId> Ids, Optional<ResolutionHints> Hints);
