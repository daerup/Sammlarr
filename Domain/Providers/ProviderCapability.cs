namespace Domain.Providers;

[Flags]
internal enum ProviderCapability
{
	Search = 1,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Trending = 4,
	Rating = 8,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Feed = 16,

	[ConvertsTo<ProviderCapability>(Rating)]
	ApiKey = 32,

	Auth = 64,
	Artwork = 128,
	Enricher = 256,
}
