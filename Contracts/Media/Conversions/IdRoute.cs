using Domain.Providers;

namespace Contracts.Media.Conversions;

public readonly record struct IdRoute(Optional<MediaProvider> From, MediaProvider To, ValueSet<MediaProvider> Via, int Calls);
