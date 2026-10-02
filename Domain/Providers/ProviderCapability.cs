namespace Domain.Providers;

[Flags]
internal enum ProviderCapability
{
	Search = 1,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Trending = 4,

	[ConvertsTo<ProviderCapability>(ApiKey)]
	Feed = 16,

	ApiKey = 32,

	Auth = 64,
	Artwork = 128,
	Enricher = 256,
}
