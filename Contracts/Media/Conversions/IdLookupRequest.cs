using Domain.Media;
using Domain.Media.Conversions;

namespace Contracts.Media.Conversions;

public union IdLookupRequest(ByIdRequest, ByHintsRequest);

public readonly record struct ByIdRequest(ById Lookup, IExternalMediaId Id);

public readonly record struct ByHintsRequest(ByHints Lookup, ResolutionHints Hints, IReadOnlyList<IExternalMediaId> Known);
