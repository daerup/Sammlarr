using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;

namespace API.Tests;

internal static class ConverterRequests
{
	internal static IdLookupRequest RequestById(this IExternalIdConverter converter, IExternalMediaId id)
	{
		var lookup = converter
			.Lookups.SelectMany<IdLookup, ById>(each =>
				each switch
				{
					ById byId => [byId],
					ByHints => [],
				}
			)
			.Single(byId => byId.Accepts.Contains(id.Source));
		return new ByIdRequest(lookup, id);
	}

	internal static IdLookupRequest RequestByHints(this IExternalIdConverter converter, IdentifyHints hints, params IExternalMediaId[] known)
	{
		var lookup = converter
			.Lookups.SelectMany<IdLookup, ByHints>(each =>
				each switch
				{
					ByHints byHints => [byHints],
					ById => [],
				}
			)
			.Single();
		return new ByHintsRequest(lookup, hints, known);
	}
}
