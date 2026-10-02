using Contracts.Media.Conversions;
using Domain.Media.Conversions;
using Domain.Providers;

namespace Core.Media;

internal union Hop(IdToId, IdToHints, HintsToId)
{
	public IExternalIdConverter Converter
	{
		get
		{
			return this switch
			{
				IdToId id => id.Converter,
				IdToHints hints => hints.Converter,
				HintsToId search => search.Converter,
			};
		}
	}

	public int Cost
	{
		get
		{
			return this switch
			{
				IdToId id => id.Lookup.Cost,
				IdToHints hints => hints.Lookup.Cost,
				HintsToId search => search.Lookup.Cost,
			};
		}
	}
}

internal readonly record struct IdToId(IExternalIdConverter Converter, ById Lookup, MediaProvider From, MediaProvider To);

internal readonly record struct IdToHints(IExternalIdConverter Converter, ById Lookup, MediaProvider From);

internal readonly record struct HintsToId(IExternalIdConverter Converter, ByHints Lookup, MediaProvider To);
