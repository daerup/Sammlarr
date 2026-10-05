using Roslyn.Providers;

namespace Roslyn.Tests;

public class ProviderMatrixGeneratorTests
{
	private const string Sample = """
		using System;
		using System.ComponentModel;
		using Shared;

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
		}

		public enum MediaProvider
		{
			[Description("TMDB")]
			[Capabilities<ProviderCapability>(ProviderCapability.Trending | ProviderCapability.Feed | ProviderCapability.ApiKey | ProviderCapability.Artwork)]
			Tmdb,

			[Description("IMDb")]
			Imdb,

			[Description("Plex")]
			[Capabilities<ProviderCapability>(ProviderCapability.Artwork)]
			Plex,

			[Description("Mdblist")]
			[Capabilities<ProviderCapability>(ProviderCapability.Feed | ProviderCapability.ApiKey)]
			Mdblist,
		}
		""";

	[Test]
	public async Task Generate_ForProvidersWithCapabilities_EmitsTheSubsetEnumsAndTheirConversions()
	{
		await Verify(await GeneratorRun.OutputAsync(new ProviderMatrixGenerator(), Sample));
	}
}
