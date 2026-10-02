using Domain.Providers;

namespace Domain.Media.Conversions;

public union IdLookup(ById, ByHints)
{
	public ValueSet<MediaProvider> Yields
	{
		get
		{
			return this switch
			{
				ById byId => byId.Yields,
				ByHints byHints => byHints.Yields,
			};
		}
	}

	public int Cost
	{
		get
		{
			return this switch
			{
				ById byId => byId.Cost,
				ByHints byHints => byHints.Cost,
			};
		}
	}
}

public readonly record struct ById(ValueSet<MediaProvider> Accepts, ValueSet<MediaProvider> Yields, int Cost);

public readonly record struct ByHints(ValueSet<MediaProvider> Yields, int Cost);
