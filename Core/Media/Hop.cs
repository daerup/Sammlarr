using Contracts.Media.Conversions;
using Domain.Media.Conversions;
using Domain.Providers;

namespace Core.Media;

internal union Hop(IdToId, IdToHints, HintsToId)
{
	public RemoteService Remote
	{
		get
		{
			return this switch
			{
				IdToId id => id.Remote,
				IdToHints hints => hints.Remote,
				HintsToId search => search.Remote,
			};
		}
	}

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

internal readonly record struct IdToId(RemoteService Remote, IExternalIdConverter Converter, ById Lookup, MediaProvider From, MediaProvider To);

internal readonly record struct IdToHints(RemoteService Remote, IExternalIdConverter Converter, ById Lookup, MediaProvider From);

internal readonly record struct HintsToId(RemoteService Remote, IExternalIdConverter Converter, ByHints Lookup, MediaProvider To);
