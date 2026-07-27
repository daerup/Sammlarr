using static Domain.Providers.ProviderCapability;

namespace Domain.Providers;

[Flags]
internal enum ProviderCapability
{
	Search = 1,
	Watchlist = 2,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Trending = 4,
	Rating = 8,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Feed = 16,

	[ConvertsTo<ProviderCapability>(Rating)]
	ApiKey = 32,

	Auth = 64,
}
