using Domain.Media;
using Domain.Providers;

namespace Harness;

internal static class TestAddressing
{
	internal static Optional<MediaProvider> ArrLike(MediaType type) =>
		type switch
		{
			MediaType.Movie => Some.Of(MediaProvider.Tmdb),
			MediaType.Show => Some.Of(MediaProvider.Tvdb),
			MediaType.Unknown => new None(),
		};
}
