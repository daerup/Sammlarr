namespace Domain.Providers;

[Flags]
internal enum ProviderCapability
{
	[ConvertsTo<ProviderCapability>(ApiKey)]
	Trending = 1 << 1,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Feed = 1 << 2,

	ApiKey = 1 << 3,
	Artwork = 1 << 5,
	Enricher = 1 << 6,
}
