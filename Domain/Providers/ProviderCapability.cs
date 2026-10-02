namespace Domain.Providers;

[Flags]
internal enum ProviderCapability
{
	Search = 1 << 0,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Trending = 1 << 1,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Feed = 1 << 2,

	ApiKey = 1 << 3,
	Auth = 1 << 4,
	Artwork = 1 << 5,
	Enricher = 1 << 6,
}
