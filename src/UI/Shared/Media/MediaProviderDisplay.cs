using Domain.Providers;
using Humanizer;

namespace UI.Shared.Media;

internal static class MediaProviderDisplay
{
	extension(MediaProvider provider)
	{
		public string Label => provider is MediaProvider.RottenTomatoes ? "Rotten Tomatoes®" : provider.Humanize();
	}
}
