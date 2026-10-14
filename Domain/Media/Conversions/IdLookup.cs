using Domain.Providers;

namespace Domain.Media.Conversions;

public readonly union IdLookup(ById, ByHints);

public readonly record struct ById(ValueSet<MediaProvider> Accepts, ValueSet<MediaProvider> Yields, int Cost, bool GivesHints = true, bool AsksOnce = false);

public readonly record struct ByHints(ValueSet<MediaProvider> Yields, int Cost);
